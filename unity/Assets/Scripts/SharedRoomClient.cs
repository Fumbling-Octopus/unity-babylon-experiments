using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Unity Editor / desktop only. Browser player uses the Babylon.js client.
public class SharedRoomClient : MonoBehaviour
{
    public string serverUrl = "ws://127.0.0.1:2567/?room=class";
    [Serializable] class Player { public string id; public float x, z; public string color; }
    [Serializable] class Message { public string type, id, room; public Player[] players; }
    [Serializable] class InputMessage { public string type = "input"; public float dx, dz; }
    class Avatar { public GameObject obj; public Vector3 target; }
    readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    readonly Dictionary<string, Avatar> avatars = new Dictionary<string, Avatar>();
    ClientWebSocket socket;
    CancellationTokenSource lifetime;
    string myId = "", status = "Connecting...";
    float nextSend;
    bool sending;

    async void Start()
    {
        Application.runInBackground = true;
        MakeWorld();
        socket = new ClientWebSocket();
        lifetime = new CancellationTokenSource();
        try {
            await socket.ConnectAsync(new Uri(serverUrl), lifetime.Token);
            status = "Connected";
            await ReceiveLoop();
        } catch (Exception e) { incoming.Enqueue("ERROR:" + e.Message); }
    }
    async Task ReceiveLoop()
    {
        var buffer = new byte[4096];
        while (!lifetime.IsCancellationRequested && socket.State == WebSocketState.Open) {
            using (var message = new MemoryStream()) {
                WebSocketReceiveResult result;
                do {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), lifetime.Token);
                    if (result.MessageType == WebSocketMessageType.Close) {
                        incoming.Enqueue("ERROR:Disconnected. Stop and Play to reconnect."); return;
                    }
                    message.Write(buffer, 0, result.Count);
                    if (message.Length > 65536) throw new IOException("Message too large");
                } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Text)
                    incoming.Enqueue(Encoding.UTF8.GetString(message.ToArray()));
            }
        }
    }
    void Update()
    {
        // All Unity object operations stay on the main thread.
        while (incoming.TryDequeue(out var json)) {
            if (json.StartsWith("ERROR:")) {status = json; continue;}
            var m = JsonUtility.FromJson<Message>(json);
            if (m.type == "welcome") { myId = m.id; continue; }
            if (m.type != "state" || m.players == null) continue;
            var live = new HashSet<string>();
            foreach (var p in m.players) {
                live.Add(p.id);
                var target = new Vector3(p.x, .5f, p.z);
                if (!avatars.ContainsKey(p.id)) {
                    var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = p.id == myId ? "You" : "Remote player";
                    obj.transform.position = target;
                    if (ColorUtility.TryParseHtmlString(p.color, out var color)) obj.GetComponent<Renderer>().material.color = color;
                    avatars[p.id] = new Avatar {obj = obj, target = target};
                }
                avatars[p.id].target = target;
            }
            var removed = new List<string>();
            foreach (var pair in avatars) if (!live.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) {Destroy(avatars[id].obj); avatars.Remove(id);}
            status = "Online: " + avatars.Count + "    You: " + myId.Substring(0, Math.Min(4,myId.Length));
        }
        float alpha = 1 - Mathf.Exp(-12 * Time.deltaTime);
        foreach (var a in avatars.Values) a.obj.transform.position = Vector3.Lerp(a.obj.transform.position,a.target,alpha);
        if (Time.unscaledTime < nextSend || sending || socket == null || socket.State != WebSocketState.Open) return;
        nextSend = Time.unscaledTime + .05f;
        float dx = 0, dz = 0;
        if (Application.isFocused) {
            dx = (Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0) - (Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            dz = (Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0) - (Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
        }
        SendInput(dx,dz);
    }
    async void SendInput(float dx, float dz)
    {
        sending = true;
        try {
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new InputMessage {dx=dx,dz=dz}));
            await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,lifetime.Token);
        } catch(Exception e) {incoming.Enqueue("ERROR:"+e.Message);}
        finally {sending=false;}
    }
    void OnGUI()
    {
        GUI.Box(new Rect(15,15,590,110),"Shared Room - Unity desktop client");
        GUI.Label(new Rect(30,45,560,25),status);
        GUI.Label(new Rect(30,70,560,25),"WASD / arrow keys. Click Game view first.");
        GUI.Label(new Rect(30,95,560,25),serverUrl);
    }
    void MakeWorld()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = Vector3.one * 1.8f;
        ground.GetComponent<Renderer>().material.color = new Color(.15f,.22f,.30f);
        var cam = Camera.main;
        if (cam == null) {var go = new GameObject("Main Camera");cam=go.AddComponent<Camera>();go.tag="MainCamera";}
        cam.transform.position = new Vector3(0,18,-18);cam.transform.LookAt(Vector3.zero);
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.063f,.094f,.153f);
        if (FindAnyObjectByType<Light>() == null) {
            var light = new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(50,-30,0);
        }
    }
    void OnDestroy() {lifetime?.Cancel();socket?.Abort();socket?.Dispose();}
}

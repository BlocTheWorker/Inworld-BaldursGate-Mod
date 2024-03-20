using BaldursGateInworld.Manager;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WebSocketSharp;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

namespace BaldursGateInworld.Util
{
    internal class InworldSocket
    {
        private readonly string SocketAddress = "ws://127.0.0.1:{PORT}/connect";

        public string awaitingTrigger;
        private WebSocket ws;
        private bool isConnectedBackend;
        private bool isCurrentlyConnected;
        private int triggerTimestamp;
        private string currentConnectedId;
        private string lastConnected;
        private DateTime _lastDataArrived;
        public delegate void TextMessageHandler(string message);
        public event TextMessageHandler? OnReceivedText;

        public List<string> eventQueue = new List<string>();
        public List<string> messageQueue = new List<string>();

        public InworldSocket(int port)
        {
            _lastDataArrived = DateTime.Now;
            SocketAddress = SocketAddress.Replace("{PORT}", port.ToString());
        }

        public void ConnectToCharacter(string id, string playerName = "man")
        {
            if (!isConnectedBackend || ws == null)
            {
                this.Connect();
                Thread.Sleep(1000);
            }
            if (currentConnectedId != id)
            {
                AudioManager.Instance.StopEverythingAbruptly();
            }
            _lastDataArrived = DateTime.Now;
            currentConnectedId = id;
            this.Send("connect", id);
            Thread.Sleep(800);
            AudioManager.Instance.StartRecording(this.OnVoiceDataReceived);
        }

        public void TriggerEvent(string id, Dictionary<string, string> parameters)
        {
            this.SendTrigger("event", id, parameters);
        }

        public void SendMessage(string message)
        {
            this.Send("message", message);
        }

        public void Disconnect()
        {
            isCurrentlyConnected = false;
            currentConnectedId = string.Empty;
            AudioManager.Instance.StopEverythingAbruptly();
            this.Send("disconnect", string.Empty);
        }

        public void ReconnectToCharacter(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            this.Send("reconnect", id);
            Thread.Sleep(800);
        }

        private void OnVoiceDataReceived(byte[] chunk)
        {
            if (ws == null || !ws.IsAlive) return;
            if (chunk == null || chunk.Length == 0) return;
            ws.Send(chunk);
        }

        public void TriggerLocationIfNeeded()
        {
            if (ws == null) return;
            if (!ws.IsAlive) return;
            if (!isCurrentlyConnected) return;
            // If player haven't interacted with the AI for 10 seconds, try to get trigger.
            if (DateTime.Now.Subtract(_lastDataArrived) > TimeSpan.FromSeconds(40))
            {
                string triggerPlace = WorldManager.Instance.IsCloseToAnything();
                if (!string.IsNullOrEmpty(triggerPlace))
                {
                    Logger.Instance.Log("[TRIGGER] Want to trigger with: " + triggerPlace);
                    _lastDataArrived = DateTime.Now;
                    var parameters = new Dictionary<string, string> {
                        { "place", triggerPlace }
                    };
                    this.TriggerEvent("location_chatter", parameters);
                }
            }
        }

        public void Connect()
        {
            try
            {
                ws = new WebSocket(SocketAddress);
                ws.OnMessage += OnMessage;
                ws.OnClose += OnClose;
                ws.OnError += OnError;
                ws.Connect();
                isConnectedBackend = true;
            }
            catch (Exception e)
            {
                Logger.Instance.Log("[SOCKET] " + e.Message);
                isConnectedBackend = false;
            }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            isConnectedBackend = false;
            ws = null;
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            isConnectedBackend = false;
            ws = null;
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                JObject msgObject = JObject.Parse(e.Data);
                if (msgObject.ContainsKey("type") && msgObject["type"].ToString() != null)
                {
                    string msgType = msgObject["type"].ToString().ToLower();
                    if (string.IsNullOrEmpty(msgType)) return;
                    if (msgType == "audio")
                    {
                        AudioManager.Instance.PushChunk(msgObject["data"].ToString());
                        _lastDataArrived = DateTime.Now;
                    }
                    else if (msgType == "text")
                    {
                        OnReceivedText?.Invoke(msgObject["data"].ToString());
                        _lastDataArrived = DateTime.Now;
                    }
                    else if (msgType == "connected")
                    {
                        Logger.Instance.Log(msgObject["data"].ToString());
                        OnReceivedText?.Invoke(msgObject["data"].ToString());
                        isCurrentlyConnected = true;
                        _lastDataArrived = DateTime.Now;
                    }
                    else if (msgType == "event")
                    {
                        string id = msgObject["event_id"].ToString();
                        eventQueue.Add(id);
                        _lastDataArrived = DateTime.Now;
                    }
                    else if (msgType == "disconnected")
                    {
                        Logger.Instance.Log("Character stopped listening. Reconnecting..");
                        ReconnectToCharacter(currentConnectedId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("[SOCKET] " + ex.Message);
            }
        }

        public void Send(string messageType, string messageData)
        {
            dynamic jsonPayload = new
            {
                type = messageType,
                message = messageData,
            };

            string jsonString = JsonConvert.SerializeObject(jsonPayload);

            if (jsonString == null || jsonString.Length == 0) return;
            if (ws == null) return;
            ws.Send(jsonString);
        }

        public void SendTrigger(string messageType, string triggerId, Dictionary<string, string> parameters)
        {
            dynamic jsonPayload = new
            {
                type = messageType,
                message = triggerId,
                parameters = parameters.ToArray()
            };

            string jsonString = JsonConvert.SerializeObject(jsonPayload);

            if (jsonString == null || jsonString.Length == 0) return;
            if (ws == null) return;
            ws.Send(jsonString);
        }
    }
}

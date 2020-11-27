using SharpConnect;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetServer : MonoBehaviour {

    const int PORT_NUM = 10000;
    private Hashtable clients = new Hashtable();
    private TcpListener listener;
    private Thread listenerThread;

    private bool isRunning = true;

    void Start()
    {
        listenerThread = new Thread(new ThreadStart(DoListen));
        listenerThread.Start();

        StartCoroutine(WaitEnd());
    //    listenerThread.Join();
    //    CloseNetwork();
    }

    IEnumerator WaitEnd()
    {
        yield return new WaitUntil(() => { return !isRunning; });
        //SendMessage("CloseApplication");
        CloseNetwork();
    }

    public void CloseNetwork()
    {
        isRunning = false;
        // 关闭全部连接
        ForEachClient((peer) => { peer.CloseConnection(); /*clients.Remove(peer.Name); */});
        //listenerThread.Join();  // 等它退出

        //Util.DelayExecute(this, 1.2f, () => { SendMessage("CloseApplication"); });
        this.DelayExecute(1.2f, () => { SendMessage("CloseApplication"); });
    }

    // This subroutine sends a message to all attached clients
    /*private void Broadcast(string strMessage)
    {
        Connector client;
        // All entries in the clients Hashtable are Connector so it is possible
        // to assign it safely.
        foreach (DictionaryEntry entry in clients)
        {
            client = (Connector)entry.Value;
            client.SendData(strMessage);
        }
    }*/

    private void ForEachClient(System.Action<Connector> action)
    {
        foreach(DictionaryEntry entry in clients)
        {
            action((Connector)entry.Value);
        }
    }

    // This subroutine checks to see if username already exists in the clients 
    // Hashtable.  if it does, send a REFUSE message, otherwise confirm with a JOIN.
    private void ConnectUser(string userName, Connector sender)
    {
        if (clients.Contains(userName))
        {
            ReplyToSender("REFUSE", sender);
        }
        else
        {
            sender.Name = userName;
            Debug.Log(userName + " has joined the chat.");
            clients.Add(userName, sender);
            // Send a JOIN to sender, and notify all other clients that sender joined
            ReplyToSender("JOIN", sender);

            SendToClients("CHAT|" + sender.Name + " has joined the chat.", sender);
        }
    }

    // This subroutine notifies other clients that sender left the chat, and removes
    // the name from the clients Hashtable
    private void DisconnectUser(Connector sender)
    {
        Debug.Log(sender.Name + " has left the chat.");
        SendToClients("CHAT|" + sender.Name + " has left the chat.", sender);
    //    sender.CloseConnection();
        clients.Remove(sender.Name);
    }

    // This subroutine is used a background listener thread to allow reading incoming
    // messages without lagging the user interface.
    private void DoListen()
    {
        try
        {
            // Listen for new connections.
            listener = new TcpListener(System.Net.IPAddress.Any, PORT_NUM);
            listener.Start(5);

            do
            {
                Debug.Log("ready to receive connection");
                //listener.BeginAcceptTcpClient
                // Create a new user connection using TcpClient returned by
                Connector client = new Connector(listener.AcceptTcpClient());
                // Create an event handler to allow the Connector to communicate
                // with the window.
                client.onReceive += (srcEndPoint, dstEndPoint, keyModule, keyAction, sid, bytes) => { Debug.Log(string.Format("{0},{1},{2},{3},{4}", srcEndPoint, dstEndPoint, keyModule, keyAction, sid)); OnLineReceived(client, Encoding.ASCII.GetString(bytes)); };
                //client.LineReceived += new LineReceive(OnLineReceived);
                //AddHandler client.LineReceived, AddressOf OnLineReceived;
                Debug.Log("new connection found: waiting for log-in");
            } while (isRunning);
        }
        catch (Exception ex)
        {
            Debug.Log("cancel accept " + ex.ToString());
            isRunning = false;
        }
    }

    // Concatenate all the client names and send them to the user who requested user list
    private void ListUsers(Connector sender)
    {
        Connector client;
        string strUserList;
        Debug.Log("Sending " + sender.Name + " a list of users online.");
        strUserList = "LISTUSERS";
        // All entries in the clients Hashtable are Connector so it is possible
        // to assign it safely.

        foreach (DictionaryEntry entry in clients)
        {
            client = (Connector)entry.Value;
            strUserList = strUserList + "|" + client.Name;
        }

        // Send the list to the sender.
        ReplyToSender(strUserList, sender);
    }

    // This is the event handler for the Connector when it receives a full line.
    // Parse the cammand and parameters and take appropriate action.
    private void OnLineReceived(Connector sender, string data)
    {
        Debug.Log(data);
        string[] dataArray;
        // Message parts are divided by "|"  Break the string into an array accordingly.
        // Basically what happens here is that it is possible to get a flood of data during
        // the lock where we have combined commands and overflow
        // to simplify this proble, all I do is split the response by char 13 and then look
        // at the command, if the command is unknown, I consider it a junk message
        // and dump it, otherwise I act on it
        dataArray = data.Split((char)13);
        dataArray = dataArray[0].Split((char)124);

        // dataArray(0) is the command.
        switch (dataArray[0])
        {
            case "CONNECT":
                ConnectUser(dataArray[1], sender);
                break;
            case "CHAT":
                SendChat(dataArray[1], sender);
                break;
            case "DISCONNECT":
                DisconnectUser(sender);
                break;
            case "REQUESTUSERS":
                ListUsers(sender);
                break;
            case "COMMAND":
                //CloseNetwork();
                listener.Stop();
                break;
            default:
                // Message is junk do nothing with it.
                break;
        }
    }

    // This subroutine sends a response to the sender.
    private void ReplyToSender(string strMessage, Connector sender)
    {
        sender.SendData(strMessage);
    }

    // Send a chat message to all clients except sender.
    private void SendChat(string message, Connector sender)
    {
        Debug.Log(sender.Name + ": " + message);
        SendToClients("CHAT|" + sender.Name + ": " + message, sender);
    }

    // This subroutine sends a message to all attached clients except the sender.
    private void SendToClients(string strMessage, Connector sender)
    {
        Connector client;
        // All entries in the clients Hashtable are Connector so it is possible
        // to assign it safely.
        foreach (DictionaryEntry entry in clients)
        {
            client = (Connector)entry.Value;
            // Exclude the sender.
            if (client.Name != sender.Name)
            {
                client.SendData(strMessage);
            }
        }
    }
}

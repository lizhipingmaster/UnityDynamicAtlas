// This is the client DLL class code to use for the sockServer
// include this DLL in your Plugins folder under Assets
// using it is very simple
// Look at LinkSyncSCR.cs


using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections;

namespace SharpConnect
{
    public class Connector
    {
        // 512K
        const int BUFFER_SIZE = 1024 * 512;
        const int READ_BUFFER_SIZE = 1024 * 2;

        private TcpClient client;
        private const int HEAD_LEN = 14;
        private byte[] headerBuffer = new byte[HEAD_LEN];

        private bool socketClosed = false;

        // 尽量不移动内存
        private byte[] readBuffer = new byte[READ_BUFFER_SIZE];
        private int readOffset = 0;

        private object lockHandle = new object();
        private bool pendingSend = false;

        private byte[] writeBuffer = new byte[BUFFER_SIZE];
        private int usedLen = BUFFER_SIZE;
        private int writeOffset = 0;
        private int sendOffset = 0;

        // 除了后面的byte[] bytes。就是头部，头部总长度14
        public delegate void OnConnectOrReceive(/*UInt16 calcLen*/byte srcEndPoint, byte dstEndPoint, UInt16 keyModule, UInt32 keyAction, UInt32 sid, byte[] bytes);
        public OnConnectOrReceive onConnect = null;
        public OnConnectOrReceive onReceive = null;

        System.Action<string> logHandle = null;

        public string Name { get; set; }

        public Connector() { }

        public Connector(TcpClient clt)
        {
            client = clt;
            client.GetStream().BeginRead(readBuffer, 0, READ_BUFFER_SIZE, new AsyncCallback(DoRead), 0);
        }

        #region 连接并递归接收数据
        public void fnConnectResult(string sNetIP, int iPORT_NUM)
        {
            try
            {
                // The TcpClient is a subclass of Socket, providing higher level 
                // functionality like streaming.
                client = new TcpClient(sNetIP, iPORT_NUM);
                // Start an asynchronous read invoking DoRead to avoid lagging the user interface.
                client.GetStream().BeginRead(readBuffer, 0, READ_BUFFER_SIZE, new AsyncCallback(DoRead), 0);

                onConnect.Invoke(byte.MaxValue, byte.MaxValue, 0, 0, 0, null);
            }
            catch (Exception ex)
            {
                logHandle.Invoke("exception:" + ex.ToString());
            }
        }

        private void DoRead(IAsyncResult ar)
        {
            int BytesRead;
            try
            {
                // If the remote host shuts down the Socket connection and all available data has been received, the EndRead method completes immediately and returns zero bytes
                // Finish asynchronous read into readBuffer and return number of bytes read.
                BytesRead = client.GetStream().EndRead(ar);
                if (BytesRead < 1 || socketClosed)
                {
                    // if no bytes were read server has close.  
                    socketClosed = true;
                    return;
                }

                BytesRead += (int)ar.AsyncState;
                // 有可能消息没全部接收过来
                int processOffset = 0;
                while (processOffset < BytesRead)
                {
                    var complete = true;
                    // 头部都不完整
                    if (BytesRead - processOffset < HEAD_LEN)
                    {
                        complete = false;
                    }
                    else
                    {
                        // 消息体不完整
                        UInt16 msgLen = BitConverter.ToUInt16(readBuffer, processOffset);
                        if (msgLen > BytesRead - processOffset)
                        {
                            complete = false;
                        }else
                        {
                            byte[] msg = new byte[msgLen - HEAD_LEN];
                            Array.Copy(readBuffer, processOffset + HEAD_LEN, msg, 0, msgLen - HEAD_LEN);
                            var offset = 0 + 2;
                            byte srcEndPoint = readBuffer[processOffset + (offset++)];
                            byte dstEndPoint = readBuffer[processOffset + (offset++)];
                            UInt16 keyModule = BitConverter.ToUInt16(readBuffer, processOffset + offset);
                            offset += 2;
                            UInt32 keyAction = BitConverter.ToUInt32(readBuffer, processOffset + offset);
                            offset += 4;
                            UInt32 sid = BitConverter.ToUInt32(readBuffer, processOffset + offset);
                            onReceive(srcEndPoint, dstEndPoint, keyModule, keyAction, sid, msg);

                            processOffset += msgLen;
                            // 继续循环
                        }
                    }

                    if (!complete)
                    {
                        // 提取内容
                        Buffer.BlockCopy(readBuffer, processOffset, readBuffer, 0, BytesRead - processOffset);
                        break;
                    }
                }

                var filledOffset = BytesRead - processOffset;
                // 发起新一轮的读取
                client.GetStream().BeginRead(readBuffer, filledOffset, READ_BUFFER_SIZE-filledOffset, DoRead, filledOffset);
            }
            catch (Exception e)
            {
                logHandle.Invoke("exception:" + e.ToString());
            }
        }
        #endregion

        #region 发送数据
        public void SendData(byte srcPoint, byte dstPoint, UInt16 keyModule, UInt32 keyAction, UInt32 sid, byte[] bytes)
        {
            var stream = client.GetStream();
            if (stream != null)
            {
                StreamWriter writer = new StreamWriter(stream);
                int msgFullLen = bytes.Length + HEAD_LEN;
                var temp = BitConverter.GetBytes((UInt16)msgFullLen);
                var offset = 0;
                Array.Copy(temp, 0, headerBuffer, offset, temp.Length);
                offset += temp.Length;
                headerBuffer[offset++] = srcPoint;
                headerBuffer[offset++] = dstPoint;

                temp = BitConverter.GetBytes(keyModule);
                Array.Copy(temp, 0, headerBuffer, offset, temp.Length);
                offset += temp.Length;

                temp = BitConverter.GetBytes(keyAction);
                Array.Copy(temp, 0, headerBuffer, offset, temp.Length);
                offset += temp.Length;

                temp = BitConverter.GetBytes(sid);
                Array.Copy(temp, 0, headerBuffer, offset, temp.Length);
                //offset += temp.Length;

                // 这个是必须的吗？
                lock (writeBuffer)
                {
                    var endPos = writeOffset + msgFullLen;
                    var sufficientBuffer = true;
                    if (endPos > BUFFER_SIZE)
                    {
                        usedLen = writeOffset;
                        writeOffset = 0;
                        logHandle.Invoke("buffer reset to head...");

                        if (writeOffset + msgFullLen > sendOffset)
                        {
                            sufficientBuffer = false;
                        }
                    }

                    // 如果缓存放不下怎么办?
                    if (sufficientBuffer)
                    {
                        // 内容写入
                        Buffer.BlockCopy(bytes, 0, writeBuffer, writeOffset + HEAD_LEN, bytes.Length);
                        // 头部写入
                        Buffer.BlockCopy(headerBuffer, 0, writeBuffer, writeOffset, headerBuffer.Length);

                        writeOffset += msgFullLen;
                    }
                    else
                    {
                        // 加另一个缓冲区
                    }
                }

                lock (lockHandle)
                {
                    if (!pendingSend)
                    {
                        StartSend(stream);
                    }
                }
            }
        }

        // Use a StreamWriter to send a message to server.
        public void SendData(string data)
        {
            SendData(0, 1, 2, 3, 4, Encoding.ASCII.GetBytes(data));
        }

        private void DoWrite(IAsyncResult ar)
        {
            try
            {
                var stream = client.GetStream();
                stream.EndWrite(ar);
                sendOffset += (int)(ar.AsyncState);
                if (socketClosed) return;

                lock (lockHandle)
                {
                    pendingSend = false;
                    if (writeOffset != sendOffset)
                    {
                        // 发起新一轮的发送
                        StartSend(stream);
                    }
                }
            }
            catch (Exception e)
            {
                logHandle(e.ToString());
            }
        }

        private void StartSend(NetworkStream stream)
        {
            pendingSend = true;
            // 折返到头部
            if (writeOffset <= sendOffset)
            {
                // 尾部发送完了。折返到头部
                if (sendOffset >= usedLen)
                {
                    sendOffset = 0;
                    stream.BeginWrite(writeBuffer, sendOffset, writeOffset - sendOffset, DoWrite, writeOffset - sendOffset);
                }
                else
                {
                    stream.BeginWrite(writeBuffer, sendOffset, usedLen - sendOffset, DoWrite, usedLen - sendOffset);
                }
            }
            else
            {
                stream.BeginWrite(writeBuffer, sendOffset, writeOffset - sendOffset, DoWrite, writeOffset - sendOffset);
            }
        }
        #endregion

        public void CloseConnection()
        {
            socketClosed = true;
            this.client.Close();
        }
    }
}
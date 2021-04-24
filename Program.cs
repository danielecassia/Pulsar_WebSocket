using System;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace PulsarWebSocket
{
    class Program
    {
        [Option(Description = "Operation Mode", LongName = "mode", ShortName = "m")]
        private string Mode { get; } = "p";

        [Option(Description = "Operation Mode", LongName = "server", ShortName = "s")]
        private string Server { get; } = "localhost";

        public static Task Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);
        private async Task OnExecuteAsync()
        {
            if (Mode == "p")
            {
                System.Console.WriteLine("Produce Mode");
            }
            else
            {
                System.Console.WriteLine("Consumer Mode");
            }

            var cts = new CancellationTokenSource();

            var socket = new ClientWebSocket();
            
            var h = Server;

            System.Console.WriteLine($"Connecting {h}...");
            
            var sw = Stopwatch.StartNew();
            if (Mode == "c")
            {
                var q = "?pullMode=true&receiverQueueSize=1";
                await socket.ConnectAsync(new Uri($"ws://{h}:8080/ws/v2/consumer/persistent/public/default/test01/sub01{q}"), cts.Token);
            }
            else
            {
                await socket.ConnectAsync(new Uri($"ws://{h}:8080/ws/v2/producer/persistent/public/default/test01"), cts.Token);
            }

            

            System.Console.WriteLine(socket.State);
            sw.Stop();
            System.Console.WriteLine($"{sw.Elapsed.Seconds}s:{sw.Elapsed.Milliseconds}ms");

            var s = new SemaphoreSlim(0);

            _ = Task.Factory.StartNew(
                async () =>
                {
                    // System.Console.WriteLine("Receive");
                    var rcvBytes = new byte[2128];
                    var rcvBuffer = new ArraySegment<byte>(rcvBytes);
                    while (true)
                    {
                        try
                        {
                            WebSocketReceiveResult rcvResult = await socket.ReceiveAsync(rcvBuffer, cts.Token);
                            s.Release();
                            byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                            string rcvMsg = Encoding.UTF8.GetString(msgBytes);
                            Console.WriteLine("Received: {0}", rcvMsg);

                            if (Mode == "c")
                            {
                                var msg = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageReceive>(rcvMsg);
                                var ack = new { messageId = msg.MessageId };
                                // System.Console.WriteLine($"Ack {msg.MessageId}");
                                var sendBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(ack)));
                                sw.Restart();
                                await socket.SendAsync(sendBuffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: cts.Token);
                                System.Console.WriteLine($"Ack done at {sw.Elapsed.Seconds}s:{sw.Elapsed.Milliseconds}ms");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            System.Console.WriteLine(ex.Message);
                            throw;
                        }
                    }
                }
            , cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);


            var ss = Stopwatch.StartNew();
            while (ss.Elapsed.TotalSeconds < 60)// (true)
            {
                var content = DateTime.Now.Ticks.ToString();// Console.ReadLine();
                if (content == "")
                {
                    socket.Dispose();
                    cts.Cancel();
                    return;
                }

                byte[] sendBytes = null;

                if (Mode == "p")
                {

                    var message = new
                    {
                        payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
                        properties = new {
                            key1 = "value1",
                            key2 = "value2"
                        },
                        context = 5
                    };
                    sendBytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(message));

                }
                else
                {

                    var message = new
                    {
                        type = "permit",
                        permitMessages = 1
                    };
                    sendBytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(message));

                }

                var sendBuffer = new ArraySegment<byte>(sendBytes);
                sw.Restart();
                await socket.SendAsync(sendBuffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: cts.Token);
                System.Console.WriteLine($"Post at {sw.Elapsed.Seconds}s:{sw.Elapsed.Milliseconds}ms");
                await s.WaitAsync();
            }

        }
    }
    public class MessageReceive
    {
        public string MessageId { get; set; }
    }
}

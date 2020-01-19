using System;
using WebSocket4Net;
using CoinbasePro.Shared.Utilities.Extensions;
using CoinbasePro.WebSocket.Models.Request;
using CoinbasePro.WebSocket.Models.Response;
//using Serilog;
using CoinbasePro.WebSocket.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IO;
using CoinbaseUtils;

namespace CoinbaseConsole
{
    public static class JsonConfig
    {
        private static JsonSerializerSettings SerializerSettings { get; } = new JsonSerializerSettings
        {
            FloatParseHandling = FloatParseHandling.Decimal,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            Error = delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args)
            {
                if (args.CurrentObject == args.ErrorContext.OriginalObject)
                {
                    Log.Error("Json serialization error {rgs.ErrorContext.OriginalObject} {args.ErrorContext.Member} {args.ErrorContext.Error.Message}");
                }
            }
        };

        internal static string SerializeObject(object value)
        {
            return JsonConvert.SerializeObject(value, SerializerSettings);
        }

        internal static T DeserializeObject<T>(string contentBody)
        {
            return JsonConvert.DeserializeObject<T>(contentBody, SerializerSettings);
        }
    }

    public class Log
    {
        public static Log Instance = new Log(DefaultLogger());
        public static TextWriter DefaultLogger()
        {
            return new StreamWriter("socket.log", true);
        }
        public TextWriter output;
        public Log(TextWriter output)
        {
            this.output = output;
        }
        internal static void Error(string message)
        {
            Instance.output.WriteLine($"[{DateTime.UtcNow.ToJson()}] [Error] {message}");
            Instance.output.Flush();
        }

        internal static void Information(string message)
        {
            Instance.output.WriteLine($"[{DateTime.UtcNow.ToJson()}] [Info] {message}");
            Instance.output.Flush();
        }
    }
    public class WebSocketFeedLogger
    {

        public WebSocketFeedLogger()
        {
        }

        public void LogMessageRecieved(MessageReceivedEventArgs e)
        {

            var json = e.Message;
            if (!json.TryDeserializeObject<BaseMessage>(out var response))
            {
                Log.Error("Could not deserialize response because the type doesn't exist {json}.");
            }

            switch (response?.Type)
            {
                case ResponseType.Subscriptions:
                    var subscription = JsonConfig.DeserializeObject<Subscription>(json);
                    //Log.Information($"{nameof(Subscription)}: {json}");
                    break;
                case ResponseType.Ticker:
                    var ticker = JsonConfig.DeserializeObject<Ticker>(json);
                    Log.Information($"{nameof(Ticker)}: {json}");
                    break;
                case ResponseType.Snapshot:
                    var snapshot = JsonConfig.DeserializeObject<Snapshot>(json);
                    Log.Information($"{nameof(Snapshot)}: {json}");
                    break;
                case ResponseType.L2Update:
                    var level2 = JsonConfig.DeserializeObject<Level2>(json);
                    Log.Information($"{nameof(Level2)}: {json}");
                    break;
                case ResponseType.Heartbeat:
                    var heartbeat = JsonConfig.DeserializeObject<Heartbeat>(json);
                    Log.Information($"{nameof(Heartbeat)}: {json}");
                    break;
                case ResponseType.Received:
                    var received = JsonConfig.DeserializeObject<Received>(json);
                    Log.Information($"{nameof(Received)}: {json}");
                    break;
                case ResponseType.Open:
                    var open = JsonConfig.DeserializeObject<Open>(json);
                    Log.Information($"{nameof(Open)}: {json}");
                    break;
                case ResponseType.Done:
                    var done = JsonConfig.DeserializeObject<Done>(json);
                    Log.Information($"{nameof(Done)}: {json}");
                    break;
                case ResponseType.Match:
                    var match = JsonConfig.DeserializeObject<Match>(json);
                    Log.Information($"{nameof(Match)}: {json}");
                    break;
                case ResponseType.LastMatch:
                    var lastMatch = JsonConfig.DeserializeObject<LastMatch>(json);
                    Log.Information($"{nameof(LastMatch)}: {json}");
                    break;
                case ResponseType.Error:
                    var error = JsonConfig.DeserializeObject<Error>(json);
                    Log.Information($"{nameof(Error)}: {json}");
                    break;
                case ResponseType.Change:
                    var change = JsonConfig.DeserializeObject<Change>(json);
                    Log.Information($"{nameof(Error)}: {json}");
                    break;
                case ResponseType.Activate:
                    var activate = JsonConfig.DeserializeObject<Activate>(json);
                    Log.Information($"{nameof(Activate)}: {json}");
                    break;
                default:
                    Log.Error("Unknown ResponseType {json}. Ignoring message received.");
                    Log.Information($"Unknown: {json}");
                    break;
            }
        }
    }
}
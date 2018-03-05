using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

// Based on SharpPad library with some adjustments.
static class Output {
  private static async Task Main(string[] args) {
    if (args.Length == 0) {
      Console.WriteLine("No launch argument specified. Usage: dotnet run <entry>");
      return;
    }

    var arg = args[0];
    Type type;
    try {
      type = Assembly.GetExecutingAssembly().GetType(arg, true, true);
    } catch {
      Console.WriteLine($"Could not locate entry type '{arg}'.");
      return;
    }

    var msg = $"No valid entry point in type '{arg}'.";
    MethodInfo main;
    try {
      main = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
    } catch {
      Console.WriteLine(msg);
      return;
    }

    if (main == null) {
      Console.WriteLine(msg);
      return;
    }

    var mainArgs = main.GetParameters();
    if (mainArgs.Length > 1) {
      Console.WriteLine(msg);
      return;
    }

    Clear();
    try {
      object result;
      if (mainArgs.Length == 1) {
        if (mainArgs[0].ParameterType != typeof(string[])) {
          Console.WriteLine(msg);
          return;
        }

        result = main.Invoke(null, new object[] { args.Skip(1).ToArray() });
      } else {
        result = main.Invoke(null, new object[0]);
      }

      if (result is Task t) {
        await t;
      }
    } finally {
      await Task.WhenAny(QueueFlush, Task.Delay(2000));
    }
  }

  private static bool useConsole;

  private static readonly Queue<Func<Task>> WorkQueue = new Queue<Func<Task>>();

  private static readonly JsonSerializerSettings DumpSettings = new JsonSerializerSettings {
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    DateFormatHandling = DateFormatHandling.IsoDateFormat,
    NullValueHandling = NullValueHandling.Include,
    TypeNameHandling = TypeNameHandling.All,
    Formatting = Formatting.None,
    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
    Converters = { new StringEnumConverter() }
  };

  private static readonly JsonSerializerSettings ConsoleSettings = new JsonSerializerSettings {
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    DateFormatHandling = DateFormatHandling.IsoDateFormat,
    NullValueHandling = NullValueHandling.Include,
    Formatting = Formatting.Indented,
    Converters = { new StringEnumConverter() }
  };

  private static readonly HttpClient HttpClient = new HttpClient();

  private static string Endpoint => $"http://localhost:{Port}";

  public static Task QueueFlush { get; private set; } = Task.CompletedTask;

  public static int Port { get; set; } = 5255;

  public static T Dump<T>(this T self) => Dump(self, null);

  public static T Dump<T>(this T self, string title) {
    var dump = JsonConvert.SerializeObject(self, DumpSettings);
    var console = JsonConvert.SerializeObject(self, ConsoleSettings);
    lock (WorkQueue) {
      WorkQueue.Enqueue(() => DumpInternal(dump, console, title));
      if (WorkQueue.Count == 1) {
        var oldFlush = QueueFlush;
        QueueFlush = Task.Run(() => Loop(oldFlush));
      }
    }

    return self;
  }

  public static void Write(string text) {
    Html("<div>" + System.Web.HttpUtility.HtmlEncode(text) + "</div>", null);
  }

  public static void Html(string html) => Html(html, null);

  public static void Html(string html, string title) {
    lock (WorkQueue) {
      WorkQueue.Enqueue(() => HtmlInternal(html, title));
      if (WorkQueue.Count == 1) {
        var oldFlush = QueueFlush;
        QueueFlush = Task.Run(() => Loop(oldFlush));
      }
    }
  }

  public static void Clear() {
    lock (WorkQueue) {
      var count = WorkQueue.Count;
      WorkQueue.Clear();
      WorkQueue.Enqueue(ClearInternal);
      if (count == 0) {
        var oldFlush = QueueFlush;
        QueueFlush = Task.Run(() => Loop(oldFlush));
      }
    }
  }

  private static async Task DumpInternal(string dump, string console, string title) {
    if (useConsole) {
      if (!string.IsNullOrEmpty(title)) {
        Console.WriteLine("--- " + title + " ---");
      }

      Console.WriteLine(console);
      Console.WriteLine();
    } else {
      await Post(DumpContainer(dump, title));
    }
  }

  private static async Task HtmlInternal(string html, string title) {
    if (useConsole) {
      if (!string.IsNullOrEmpty(title)) {
        Console.WriteLine("--- " + title + " ---");
      }

      Console.WriteLine("<<Install SharpPad extension to display html>>");
      Console.WriteLine();
    } else {
      var payload = new JObject(
          new JProperty("$type", "html"),
          new JProperty("$html", html));
      await Post(DumpContainer(payload.ToString(), title));
    }
  }

  private static async Task ClearInternal() {
    if (!useConsole) {
      await HttpClient.GetAsync($"{Endpoint}/clear");
    }
  }

  private static string DumpContainer(string value, string title) {
    return $"{{\"$type\":\"DumpContainer, SharpPad\",\"source\":null,\"title\":{JsonConvert.SerializeObject(title)},\"$value\":{value}}}";
  }

  private static Task Post(string message) {
    var content = new StringContent(message);
    content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
    return HttpClient.PostAsync(Endpoint, content);
  }

  private static async Task Loop(Task oldFlush) {
    await oldFlush;
    while (true) {
      Func<Task> item;
      lock (WorkQueue) {
        if (WorkQueue.Count == 0) {
          return;
        }

        item = WorkQueue.Dequeue();
      }

      try {
        await item();
      } catch {
        useConsole = true;
      }
    }
  }
}

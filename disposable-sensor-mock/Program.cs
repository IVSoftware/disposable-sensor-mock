using System.ComponentModel;

Console.Title = "Disposable Sensor";
using (var sensor = new Sensor())
{
    sensor.Disposed += OnSensorDisposed;
    sensor.SensorUpdated += OnSensorUpdate;
    sensor.PropertyChanged += (sender, e) => Console.WriteLine($"{DateTime.Now} Property Changed: {e.PropertyName}");
    await Task.Delay(TimeSpan.FromSeconds(10));
}

Console.WriteLine($"{DateTime.Now} Disposing...");
// To observe post-destroy behavior
await Task.Delay(TimeSpan.FromSeconds(10));
Console.WriteLine($"{DateTime.Now} Press any key");
Console.ReadKey();
void OnSensorUpdate(object? sender, EventArgs e)
{
    // Remember to marshal onto UI thread if client is running one e.g. BeginInvoke(()=>{...});
    if(sender is Sensor sensor) Console.WriteLine($"{sensor.Reading}");
}
void OnSensorDisposed(object? sender, EventArgs e)
{
    Console.WriteLine($"{DateTime.Now} Sensor has completed its final disposal.");
}

class Sensor : IDisposable, INotifyPropertyChanged
{
    public Sensor() => PollingTask = Task.Run(async () =>
    {
        while (!IsDisposing)
        {
            Reading = DateTimeOffset.Now.ToString();
            SensorUpdated?.Invoke(this, EventArgs.Empty);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reading)));
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        // Waits an arbitrary 5 seconds before returning from Task to test the awaiter.
        await Task.Delay(TimeSpan.FromSeconds(5));
    });
    public async void Dispose()
    {
        try // Avoid the remote possibility of an unhandled exception caused by shutting down.
        {            
            IsDisposing = true; // This will terminate the polling loop.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDisposing)));
            Console.WriteLine($"{DateTime.Now} Polling loop ends here.");
            Console.WriteLine($"{DateTime.Now} An arbitrary 5 seconds is in the task to prove the awaiter works.");
            await PollingTask;
            PollingTask?.Dispose();
            Disposed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, new DisposingExceptionEventArgs(ex));
        }
    }
    Task PollingTask { get; }
    public string? Reading { get; private set; }
    public bool IsDisposing { get; private set; }
    public class DisposingExceptionEventArgs : EventArgs
    {
        public DisposingExceptionEventArgs(Exception ex) => HandledException = ex;
        public Exception HandledException { get; }
    }
    public event EventHandler? Error;
    public event EventHandler? Disposed;
    public event EventHandler? SensorUpdated;
    public event PropertyChangedEventHandler? PropertyChanged;
}
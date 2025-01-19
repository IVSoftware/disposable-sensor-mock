using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BBuster
{
    [TestClass]
    public sealed class TestMyAnswer
    {
        /// <summary>
        /// Tests the basic disposable pattern using the SensorPollWorldTimeAPI class.
        /// This method sets up a Sensor instance with specified intervals and timeouts,
        /// subscribes to various events (e.g., SensorUpdated, Error, and PropertyChanged),
        /// and performs a series of operations to monitor sensor behavior.
        /// 
        /// The method demonstrates:
        /// - Configuring the sensor to simulate periodic readings with a timeout.
        /// - Logging sensor updates, errors, and property changes.
        /// - Canceling execution after a set number of successful reads.
        /// 
        /// The test runs the sensor for up to one hour or until 10 successful readings are reached,
        /// ensuring robust behavior under specified conditions.
        /// 
        /// Expected behavior includes:
        /// - Logging successful and failed readings.
        /// - Advising the user to adjust the timeout for testing failure conditions.
        /// - Handling expected cancellation and unexpected exceptions gracefully.
        /// </summary>
        /// <remarks>
        /// This test relies on the Sensor class supporting events for updates, errors,
        /// and property changes, and a cancellation mechanism for controlled execution.
        /// </remarks>
        [TestMethod]
        public async Task Test_BasicDisposablePattern()
        {
            try
            {
                // NOTE:
                // On 'this' system, this time out setting is providing a
                // mix of successful readings and time out cancellations.
                // "YOUR MILEAGE MAY VARY".
                using (
                    var sensor = new SensorPollWorldTimeAPI(
                    interval: TimeSpan.FromSeconds(1),
                    timeOut: TimeSpan.FromSeconds(2)))
                using (var cts = new CancellationTokenSource())
                {
                    sensor.SensorUpdated += (sender, e) =>
                    {
                        UtilLogMessage($"{sensor.Reading} Succeeded: {sensor.SuccessCount} Failed: {sensor.ErrorCount}");
                    };
                    sensor.Error += (sender, e) =>
                    {
                        UtilLogMessage(sensor.LastException?.Message);
                    };
                    sensor.PropertyChanged += (sender, e) =>
                    {
                        switch (e.PropertyName)
                        {
                            case nameof(SensorPollWorldTimeAPI.SuccessCount):
                                if(sensor.SuccessCount >= 10)
                                {
                                    // Exit after 10 successfull reads.
                                    if(sensor.ErrorCount == 0)
                                    {
                                        UtilLogMessage("ADVISORY: Consider reducing the time out to induce failures.");
                                    }
                                    cts.Cancel();
                                }
                                break;
                        }
                    };
                    // Run the sensor for an arbitrary amount of time.                    
                    await Task.Delay(TimeSpan.FromHours(1), cts.Token);
                }
            }
            catch(OperationCanceledException)
            {
                UtilLogMessage("ADVISORY: This is an expected cancellation of the '1 hour delay'.");
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        [TestMethod]
        public async Task Test_UserKnowsWhenTaskIsComplete()
        {
            SemaphoreSlim CanShutdown = new SemaphoreSlim(0, 1);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                using (
                    var sensor = new SensorPollWorldTimeAPI(
                    interval: TimeSpan.FromSeconds(1),
                    timeOut: TimeSpan.FromSeconds(2)))
                {
                    sensor.Disposed += (sender, e) =>
                    {
                        CanShutdown.Release();
                    };
                    UtilLogMessage($"{DateTime.Now} Starting the task. Total Seconds {sw.Elapsed.TotalSeconds}");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                await localMockApplicationExit();

                #region L o c a l F x
                async Task localMockApplicationExit()
                {
                    UtilLogMessage($"{DateTime.Now} Disposing! Total Seconds {sw.Elapsed.TotalSeconds}");
                    Assert.IsTrue(
                        sw.Elapsed.TotalSeconds > 4 && sw.Elapsed.TotalSeconds < 6,
                        "Expecting mock shutdown to be called ~5 seconds in when `using` block exits.");
                    UtilLogMessage($"{DateTime.Now} Waiting for sensor polling to complete...");
                    await CanShutdown.WaitAsync();
                    sw.Stop();

                    Assert.IsTrue(
                        sw.Elapsed.TotalSeconds > 10 && sw.Elapsed.TotalSeconds < 15, 
                        "Expecting total runtime to be ~10 seconds.");

                    UtilLogMessage($"{DateTime.Now} SAFE to shutdown. Total Seconds {sw.Elapsed.TotalSeconds}");
                }
                #endregion L o c a l F x
            }
            finally
            {
                CanShutdown.Wait(0);
                CanShutdown.Release();
                CanShutdown.Dispose();
            }
        }

        [TestMethod]
        public async Task Test_IsTheThingStillViableObjectAfterDispose()
        {
            SensorPollWorldTimeAPI? objectToInspect = null;
            using (
                var sensor = new SensorPollWorldTimeAPI(
                interval: TimeSpan.FromSeconds(1),
                timeOut: TimeSpan.FromSeconds(2)))
            {
                objectToInspect = sensor;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            UtilLogMessage("Proving that the object reference is NOT disposed BEFORE FORCED GC.");
            Assert.IsNotNull(objectToInspect, "Expecting non-null instance after leaving 'using' block.");
            Assert.IsTrue(objectToInspect is SensorPollWorldTimeAPI stillKickingA, "Expecting viable instance after leaving 'using' block.");
            GC.Collect();
            UtilLogMessage("Proving that the object reference is NOT disposed AFTER FORCED GC.");
            Assert.IsNotNull(objectToInspect, "Expecting GC to have no effect. The object reference is not disposed.");
            Assert.IsTrue(objectToInspect is SensorPollWorldTimeAPI stillKickingB, "Expecting GC to have no effect. The object reference is not disposed.");

            UtilLogMessage("Proving that the object bool remains accessible and is SET.");
            Assert.IsTrue(objectToInspect.IsDisposing, "This value is set when the Dispose block is entered.");
        }

        [TestMethod]
        public async Task Test_SomethingBadHappensInDispose()
        {
            using (
                var sensor = new SensorPollWorldTimeAPI(
                interval: TimeSpan.FromSeconds(1),
                timeOut: TimeSpan.FromSeconds(2)))
            {
                sensor.Error += (sender, e) =>
                {
                    if(e is SensorPollWorldTimeAPI.DisposingExceptionEventArgs e_d)
                    {
                        var msg = $"An exception of type {e_d.HandledException.GetType().Name} occurred in Dispose() method";

                        UtilLogMessage(msg);
                        UtilLogMessage($"Message: {e_d.HandledException.Message}");
                    }
                    else
                    {
                        UtilLogMessage(sensor.LastException?.Message);
                    }
                };
                sensor.InjectedException = new ThreadInterruptedException();
                // Run for a short time, then dispose to raise forced error in Dispose().
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private void UtilLogMessage(string? message)
        {
            Debug.WriteLine(message);
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Use HttpClient to poll WorldTimeAPI which is believed
    /// to be a bit spotty responding, which is great!
    /// </summary>
    class SensorPollWorldTimeAPI : IDisposable, INotifyPropertyChanged
    {
        public event EventHandler? SensorUpdated;
        public event EventHandler? Error;
        public Exception? LastException { get; private set; }
        public SensorPollWorldTimeAPI(TimeSpan interval, TimeSpan timeOut) => _pollingTask = Task.Run(async () =>
        {
            _readingTimeOut = timeOut;
            while (!IsDisposing)
            {
                try
                {
                    await GetSensorReadingAsync();
                    LastException = null;
                }
                catch (Exception ex)
                {
                    ErrorCount++;
                    LastException = ex;
                    Error?.Invoke(this, EventArgs.Empty);
                }
                await Task.Delay(interval);
            }
            // FOR TEST PURPOSES ONLY: Wait an arbitrary 5 seconds before
            // returning from Task to test the awaiter.
            await Task.Delay(TimeSpan.FromSeconds(5));
        });
        public async void Dispose()
        {
            try
            {
                // This terminates the polling loop.
                IsDisposing = true;
                Console.WriteLine($"{DateTime.Now} Polling loop ends here.");
                Console.WriteLine($"{DateTime.Now} An arbitrary 5 seconds is in the task to prove the awaiter works.");

                // ABOUT THE ONLY WAY TO MAKE SOMETHING GO WRONG IS TO INJECT IT LIKE SO.
                if(InjectedException != null) throw InjectedException;

                await _pollingTask;
                _pollingTask?.Dispose();
                _httpClient?.Dispose();
                Disposed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new DisposingExceptionEventArgs(ex));
            }
        }
        public event EventHandler? Disposed;

        private readonly Task _pollingTask;
        private TimeSpan _readingTimeOut;
        public bool IsDisposing
        {
            get => _isDisposing;
            private set
            {
                if (!Equals(_isDisposing, value))
                {
                    _isDisposing = value;
                    OnPropertyChanged();
                }
            }
        }
        bool _isDisposing = default;

        /// <summary>
        /// Successful readings since inception.
        /// </summary>
        public int SuccessCount
        {
            get => _updateCount;
            private set
            {
                if (!Equals(_updateCount, value))
                {
                    _updateCount = value;
                    OnPropertyChanged();
                }
            }
        }
        int _updateCount = default;

        /// <summary>
        /// Errors readings since inception.
        /// </summary>
        public int ErrorCount
        {
            get => _errorCount;
            set
            {
                if (!Equals(_errorCount, value))
                {
                    _errorCount = value;
                    OnPropertyChanged();
                }
            }
        }
        int _errorCount = default;

        /// <summary>
        /// Json or plain string reading value.
        /// </summary>
        public string Reading
        {
            get => _reading;
            private set
            {
                if (!Equals(_reading, value))
                {
                    _reading = value;
                    OnPropertyChanged();
                }
            }
        }
        string _reading = string.Empty;

        public TaskAwaiter GetAwaiter() => _pollingTask.GetAwaiter();

        /// <summary>
        /// WARNING: For testing purposes only.
        /// </summary>
        public Exception? InjectedException { get; set; }

        private readonly HttpClient _httpClient = new HttpClient();
        async Task GetSensorReadingAsync()
        {
            using (var cts = new CancellationTokenSource(_readingTimeOut))
            {
                HttpResponseMessage response = await _httpClient.GetAsync(
                    "https://worldtimeapi.org/api/timezone/Etc/UTC",
                    cts.Token
                );
                if (response.IsSuccessStatusCode)
                {
                    if (JObject.Parse(await response.Content.ReadAsStringAsync()) is { } valid)
                    {
                        if ((valid["utc_datetime"] as JToken)?.ToString() is { } utcstr &&
                            DateTimeOffset.TryParse(utcstr, out var utc))
                        {
                            SuccessCount++;
                            Reading = utc.ToLocalTime().ToString();
                            SensorUpdated?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
                else
                {
                    throw new HttpRequestException($"Failed to read target. Status code: {response.StatusCode}");
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;

        public class DisposingExceptionEventArgs : EventArgs
        {
            public DisposingExceptionEventArgs(Exception ex) => HandledException = ex;
            public Exception HandledException { get; }
        }
    }
}

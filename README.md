**This Answer has been Rolled Back**

Previously, I posted an answer grounded in my decades of experience with threading that began with the inception of [auto_ptr](https://learn.microsoft.com/en-us/cpp/standard-library/auto-ptr-class?view=msvc-170) (introduced with Visual Studio 6.0 circa 1998). 

But then, I bowed to pressure and removed it, basing this action both on the helpful suggestions and the multiple criticisms offered in the comments (and some negative feedback on the vote). However, coding is fundamentally rooted in the scientific method—claims can and should be tested. So I did that, rigorously, and have appended my unit testing to my original repo. Now I'm standing by my original answer (see #Unit Test section below) which I've tweaked a _little_ but is substantively the same.

In other words, _I don't think you can break my code_ but if you succeed in doing so, please [Prove IV Wrong](https://github.com/IVSoftware/disposable-sensor-mock/issues) and open an Issue detailing your breaking unit test. Constructive criticism is vital in a coding community. I'm sharing this in good faith, fully expect you to "vote your conscience", and know that `StackOverflow` upholds the ideals of open dialogue and the exploration of legitimate inquiries without dismissiveness. I'm not trying to be sarcastic; I'm sincerely asking you to show me any version of reality where this code doesn't work.
___
**Answer**

>Is this proper usage of IDisposable?

One common way to make use of an IDisposable class is in the context of a using block. So, for example, one way to avoid "a lot of code" would be to implement the Sensor class something like this:

~~~
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
~~~
___

**Console Mock (Part of my original answer).**

To test this out, we could just do a quick console app:

~~~
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
~~~

___

**Unit Testing**

Taking the comments seriously (some here may know that I have public NuGet packages and some involve threading) I have endeavored to take the criticisms one by one to perform comprehensive testing.
___

**Point:** "Does not provide any way for the user to know when the task is actually complete."
**Counterpoint**: This excellent point has been addressed by adding a `Disposed` event to sensor.
**Coverage**: Test_UserKnowsWhenTaskIsComplete()

[![test result PASS][1]][1]

___

**Point:** "That could easily result in race conditions if there are any external dependencies."
**Counterpoint**: `PollingTask` is private. `IsDisposing` is `private set`. The `Dispose()` method does not act upon any memory spaces that could realistically hold any external dependencies.
**Coverage**: UNABLE TO FORMULATE A UNIT TEST THAT COULD CREATE SAID CONDITION.  PLEASE PROVIDE A TEST CASE for a race condition that "could easily result".

___

**Point (1 ▲):** "The semantics of Dispose is that after this method returns, the thing is completely disposed." 
**Counterpoint**: "The semantics of Dispose is that it provides an opportunity to clean up managed and unmanaged resources without changing the reference count on the object." 
**Coverage**:  Test_IsTheThingStillViableObjectAfterDispose()

[![test result PASS][2]][2]

___

**Point:** "You just can't do asynchronous work in the Dispose. This method is synchronous."
**Counterpoint**: An `async void` method is _not_ synchronous and _can_ implement an interface method (or event handler) with a `void` return type. Few claims on this site rise to this level of disprovability.
**Coverage**: SELF_EVIDENT-LITERALLY EVERY UNIT TEST DOES "ASYNCHRONOUS WORK IN THE DISPOSE".
___

**Point: (1 ▲)** "There are tons of questions in StackOverflow where people used async void methods incorrectly, and then payed the price."
**Coverage**: NOT A SCIENTIFICALLY VERIFIABLE CLAIM AMENABLE TO DETERMINISTIC TESTING. But it does seem likely that new coders are more likely to misuse async methods (and post the resulting questions) than experienced ones. 

___

**Point: (1 ▲)** "If anything bad happens inside the async void Dispose(), the caller will not be able to handle it with try/catch."
**Counterpoint** 
1. Instead of saying "anything bad" can we all agree that an unhandled exception in Dispose would be very problematic?
2. So, on one hand, this is an easily reproducable fault that can and should be tested. 
3. However, we are talking about a _specific_ implementation of `Dispose()`. Someone _please_ demonstrate how "something bad" could happen in it (other than _adding_ a test hook to see what happend in this contrived scenario) 
4. Out of an abundance of caution, we can wrap a try-catch around the `Dispose` implementation and gracefully handle it. 
**Coverage**: Test_SomethingBadHappensInDispose()

[![test result PASS][3]][3]

___

**Point: (1 ▲)** "Making the standard Dispose method async is [...] very dangerous."
**Counterpoint** The claim that _this specific `async void Dispose()`_ is dangerous in any way, shape or form is extravagant and needs to have a hard test case behind it to be taken seriously.
**Coverage**:  PLEASE PROVIDE A TEST CASE.


___
**Point: (1 ▲)** "Also, access to the IsDisposing property needs to be synchronized as your task runs on a thread pool thread"
**Counterpoint** There is simply no conceivable way that two threads could compete over the instance `IsDisposing` property that is `private set` and that is an instance property. Even if you did something radically contrived like `static IDisposable Sensor{ get; } = new Sensor()` you still couldn't _set_ it from multiple threads.
**Coverage**: PLEASE PROVIDE A TEST CASE.

___

**Point:(1 ▲)** "There is a risk that when your code sets IsDisposing = true, the thread that runs the task might not see it".
**Counterpoint** The thread that runs the task is owned by the instance. There is simply no way that `Dispose()` could be called by _any_ rando thread where `IsDisposed` would not be seen by the polling loop and shut down.
**Coverage**: TESTABLE, BUT SEEMS LIKE AN EXTREMELY CORNER CASE. For an object whose intended use is `using(new Sensor()){ ... }` you would somehow need to give access to that unnamed `Sensor` object _and_ have that other thread call `Dispose()` on it. PLEASE PROVIDE A TEST CASE (preferably, one that has some bearing to reality).

__

**SUMMARY**

In the words of Dan Quayle (44th vice president of the United States from 1989 to 1993):

> I stand by all my misstatements.


  [1]: https://i.sstatic.net/yrAxUwa0.png
  [2]: https://i.sstatic.net/8MPQExPT.png
  [3]: https://i.sstatic.net/5SsiWQHO.png
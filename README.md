# TaskPool
TaskPool is an helper class that allow you to control how many tasks you want to be running at maximum at the same time and how many you want to be keep in a waiting queue.
For instance you want to avoid having 100 tasks at once on a little machine to avoid cap out your CPU usage and render your whole program unusable.
Usefull when you want to fire and forget a bunch of tasks for instance.

The whole fire and forget premise is the core idea being this implementation, meaning that some action like "removing an already queued task" is not permited as of today.

Please beware that this code is not production ready, use it at your own discretion.

# Installation
This project is distributed "as it". You will need to either clone this repository or copy the TaskPool class in TaskPool.cs where you want to use it in your project.

# Usage
```csharp
var taskPool = new TaskPool(10, 100); // Create a TaskPool able to running 10 tasks at the same time and queue up to 100 other ones
var task1 = new Task(() => Thread.Sleep(1000)); // create a bunch of tasks
var task1 = new Task(() => Thread.Sleep(1000));
taskPool.Queue(task1); // queue the tasks for execution, you do not need further action
taskPool.Queue(task2); // note that calls to .Queue() should be thread safe in any situation
```

# Implementation logic
This project aim to demonstrate the concept of ".ContinueWith()" recursion. Do not look it up I made the name up.
Basically every sent task to the pool will have a .ContinueWith() task attached to it to trigger the execution of the next queued ones. Because the inside of this .ContinueWith() action (see "NotifyTaskEnded"" method) will call itself I call it "recursion". However we take advantage of the fact that each previous task context is dropped once the execution is done.

# Future improvements / TODO
List of points to address, improve, or idea of future improvements to make :
- More precise test cases
- Do we throw an exception when the task provided is not in the "Created" state?
- What happen if a task is canceled outside of the TaskPool scope by the user
- Provide a true "ClearWaitingQueue()" that will mark all tasks as cancelled to then safelly dispose of the TaskPool
- Provide an Async version of Queue() method?

# Contribution
I am really open to remarks, ideas of improvements, etc, so feel free to open issues or create pull requests.
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace TaskPool
{
    /// <summary>
    /// Allow you to control how many tasks you want to be running at maximum at the same time and how many you want to be keep in waiting queue.
    /// For instance you want to avoid having 100 tasks at once on a little machine to avoid cap out your CPU usage and render your whole program unusable.
    /// Usefull when you want to fire and forget a bunch of tasks for instance.
    /// </summary>
    public class TaskPool : IDisposable
	{
		private ConcurrentQueue<Task> _waitingTasksQueue = new ConcurrentQueue<Task>();

		// Using a unique integer key to search / filter tasks is faster than going through a list en check every element id
		private ConcurrentDictionary<int, Task> _runningTasksDict = new ConcurrentDictionary<int, Task>();

		private uint _maxNumberOfRunningTasks = 10;
		private uint _maxNumberOfWaitingTasks = 1_000;

		// For dispose pattern
		private bool _disposed = false;

		// Cannot really hurt to have those available?
		public int NumberOfRunningTasks => _runningTasksDict.Count;
		public int NumberOfWaitingTasks => _waitingTasksQueue.Count;

		/// <summary>
		/// Instanciate a new TaskPool object.
		/// Try to keep only one instance of this at a time if possible.
		/// We allow to have a number of queued task at zero, however the number of running tasks must be at least one.
		/// </summary>
		/// <param name="maxNumberOfRunningTasks">The maximum of parallel running tasks (!= number of thread used !)</param>
		/// <param name="maxNumberOfWaitingTasks">The maximum number of tasks that can wait for running</param>
		public TaskPool(uint maxNumberOfRunningTasks = 10, uint maxNumberOfWaitingTasks = 1_000)
        {
			if (maxNumberOfRunningTasks == 0)
				throw new ArgumentException("Cannot have a number of maximum running tasks at zero", "maxNumberOfRunningTasks");
			_maxNumberOfRunningTasks = maxNumberOfRunningTasks;
			_maxNumberOfWaitingTasks = maxNumberOfWaitingTasks;
        }

		/// <summary>
		/// Allow you to queue a task for execution.
		/// If the number of currently running tasks is inferior to the maximum number of running tasks, it will be started right away.
		/// If an error related to the internal process encoutner an error and cannot handle the task this return false.
		/// However if the user perform a bad operation (trying to add a task to a pool without anymore space available) it will throw an exception.
		/// </summary>
		/// <param name="task">the task to queue for execution</param>
		/// <returns>true if the task was successfully queued / launched, false otherwise</returns>
		public bool Queue(Task task)
		{
			if (task == null)
				throw new ArgumentNullException("task", "Cannot queue / start a null task");

			if (task.Status == TaskStatus.Running || task.Status == TaskStatus.WaitingToRun)
				throw new ArgumentException($"Cannot queue an already launched / running task - task.Id : {task.Id}", "task");

			if (task.Status == TaskStatus.Faulted || task.Status == TaskStatus.Canceled || task.Status == TaskStatus.RanToCompletion)
				throw new ArgumentException($"Cannot queue an already runned / faulted / cancelled task - task.Id : {task.Id}", "task");

			// Even if we check thoses values just afterwards we prefer to make this exception throw before any more weighted actions like "Contains"
			if (_runningTasksDict.Count == _maxNumberOfRunningTasks && _waitingTasksQueue.Count() == _maxNumberOfWaitingTasks)
				throw new ArgumentException("The maximum number of running and waiting tasks have been reach, try to queue your task later on when any other pooled one have finished its job", "task");

			// Overall it seems that using TryAdd is more consistent than checking .ContainsKey (plus .ContainsKey needs an overhead time on first call) in therms of processor ticks
			// So its a choice of user friendly design : do we want to throw whenever a task is already enqueued or launched or just return false?
			// Even if throw (using .ContainsKey for checking) is the slowest solution perfomance wise it seems better for user interactivity : we can provide more information than just return false that tells "maybe there is a wrokload problem or maybe the key already exists" etc...
			if (_runningTasksDict.ContainsKey(task.Id))
                throw new ArgumentException("A task with the same id already exists in the TaskPool", "task.Id");
            // After some tests if you are using a classic "Queue" collection a search on element reference (i.e. using queue.Contains(task)) is more consistent and faster than using linq
			// However for some reason by using a "ConcurrentQueue" the check with linq (i.e. using queue.Any(x => x.Id == id)) is way faster
			// Note that on large volumes (in the millions) checking for an element existence in a ConcurrentQueue is a really heavy operation (nearly 25 ms with an high end processor) and may require some refactoring in here
            if (_waitingTasksQueue.Any(x => x.Id == task.Id))
				throw new ArgumentException("A task with the same id already exists in the TaskPool", "task.Id");

			// ConcurrentQueue.Count() implementation does not go through the whole collection to calculate the number of element so it is safe to invoke it every time
			// See https://referencesource.microsoft.com/#mscorlib/system/Collections/Concurrent/ConcurrentQueue.cs,f36684fa5c19fdfb for reference (performs only the operation positionLastMember - positionFirstMember)
			if (_runningTasksDict.Count < _maxNumberOfRunningTasks)
			{
				if (task.IsCanceled || !_runningTasksDict.TryAdd(task.Id, task))
					return false;

				task.ContinueWith((previous) => NotifyTaskEnded(previous.Id));
				task.Start();
			}
			else if (_waitingTasksQueue.Count() < _maxNumberOfWaitingTasks)
				_waitingTasksQueue.Enqueue(task); // Do not add the Continuation task if you are not running it right away, saving a little bit of memory and ease understanding of implementation
			else
				return false;
				// Well the check that there is space in either waiting or running collections is made upper-hand and this should not be reach by any means
				// If this method returns false (with the maximum number of running tasks reached) we might have a serious problem. Maybe an async behavior that I did not anticipate?
				// throw new ArgumentException("The maximum number of running and waiting tasks have been reach, try to queue your task later on when any other pooled one have finished its job", "task");

			return true;
		}

		// Does this serve any purpose?
		public void ClearWaitingQueue()
        {
			// See wich of going throught all queue via foreach versus TryDequeue each elem to mark tasks as canceled if the fastest one
			// In a sens TaskPool does not provide a way to manually cancel already queued tasks so a "cancel tasks on clear" behavior feels wrong
			_waitingTasksQueue.Clear();
        }

		/// <summary>
		/// Called after a task from the pool terminate.
		/// Allow to run the next queued one if any.
		/// You can safely attach this with .ContinueWith because ContinuationTask recursion does not make any problem, the previous context is always dropped.
		/// The previous tasks are always discarded so no unecessary ressource is restrained in opposition with "true" recursion.
		/// We avoid keeping alive the whole previous task in memory by only allowing the task id to be send to this method.
		/// </summary>
		/// <param name="endedTaskId">The id of the task that just ended</param>
		/// <returns>A task that can be used attached to ContinuationTask to trigger next queued one execution</returns>
		private Task NotifyTaskEnded(int endedTaskId)
		{
			return Task.Run(() =>
			{
				if (!_runningTasksDict.TryRemove(endedTaskId, out _))
					throw new ArgumentException($"Unable to remove ended task from running collection - You may end up with a capped running task collection resulting in the incapability to launch new incoming tasks - TaskId : {endedTaskId}");

				// Even if ConcurrentQueue.Count is not a huge performance hit, using "IsEmpty" is faster than ".Count == 0"
				if (!_waitingTasksQueue.IsEmpty && _runningTasksDict.Count < _maxNumberOfRunningTasks)
				{
                    Task taskToRun = null;

					while (taskToRun == null)
					{
						// If after trying to dequeue all the _waitingTasksQueue content nothing could be correctly run we exit
						if (_waitingTasksQueue.IsEmpty)
							return;

						if (!_waitingTasksQueue.TryDequeue(out taskToRun))
							throw new InvalidOperationException("The TaskPool was unable to dequeue next task to execute - This might occur in case the host machine workload is too high");

						// If the task was canceled / already started / runned / faulted we ignore it (we let GC or user destroy it)
						// TODO : statuate on beahvior when this happens
						if (taskToRun != null && taskToRun.Status != TaskStatus.Created)
							continue;
					}

					if (taskToRun == null || !_runningTasksDict.TryAdd(taskToRun.Id, taskToRun))
						throw new InvalidOperationException($"The TaskPool was unable to add the new task to run to the currently running collection - The task will not be started - TaskId : {taskToRun.Id}");

					// Attach a call to NotifyTaskEnded to trigger next task run after taskToRun terminate
					taskToRun.ContinueWith((previous) => NotifyTaskEnded(previous.Id));
					taskToRun.Start();
				}
			});
		}

        public void Dispose()
        {
			this.Dispose(true);
        }

		private void Dispose(bool disposing)
        {
			if (_disposed)
				return;
			if (disposing)
            {
				_maxNumberOfRunningTasks = 0;
				_maxNumberOfWaitingTasks = 0;

				_runningTasksDict.Clear();
				_waitingTasksQueue.Clear();

				_runningTasksDict = null;
				_waitingTasksQueue = null;
			}
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace TaskPool.Tests
{
    [TestClass]
    public class TaskPoolTests
    {
        private Task GetDummyTask() => new Task(() => Thread.Sleep(10000));
        private Task GetFastDummyTask() => new Task(() => Thread.Sleep(1));

        [TestMethod]
        public void Instanciation_DefaultValuesTest()
        {
            var taskPool = new TaskPool();
            Assert.IsNotNull(taskPool);
        }

        [TestMethod]
        public void Instanciation_ExplicitNumberOfRunningTasksTest()
        {
            var taskPool = new TaskPool(maxNumberOfRunningTasks: 100);
            Assert.IsNotNull(taskPool);
        }

        [TestMethod]
        public void Instanciation_ExplicitNumberOfWaitingTasksTest()
        {
            var taskPool = new TaskPool(maxNumberOfWaitingTasks: 100);
            Assert.IsNotNull(taskPool);
        }

        [TestMethod]
        public void Instanciation_AllParametersTest()
        {
            var taskPool = new TaskPool(10, 100);
            Assert.IsNotNull(taskPool);
        }

        [TestMethod]
        public void Instanciation_ShouldThrowIfNumberOfRunningTasksIsZero()
        {
            Assert.ThrowsException<ArgumentException>(() => new TaskPool(0, 100));
        }

        [TestMethod]
        public void Instanciation_CanInstanciateNumberOfWaitingToZeroButStillRun()
        {
            var taskPool = new TaskPool(3, 0);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
        }

        [TestMethod]
        public void Queue_ShouldThrowIfTaskIsNull()
        {
            var taskPool = new TaskPool(10, 100);
            Assert.IsNotNull(taskPool);
            Assert.ThrowsException<ArgumentNullException>(() => taskPool.Queue(null));
        }

        [TestMethod]
        public void Queue_CanQueueOneTaskTest()
        {
            var taskPool = new TaskPool(10, 100);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
        }

        [TestMethod]
        public void Queue_CanQueueOneMultipleTaskTest()
        {
            var taskPool = new TaskPool(10, 100);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
        }

        [TestMethod]
        public void Queue_ShouldThrowIfSameTaskIsAddedTwiceInRunningOnes()
        {
            var taskPool = new TaskPool(10, 100);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));

            var uniqueTask = GetDummyTask();
            Assert.IsTrue(taskPool.Queue(uniqueTask));
            Assert.ThrowsException<ArgumentException>(() => taskPool.Queue(uniqueTask));
        }

        [TestMethod]
        public void Queue_ShouldThrowIfSameTaskIsAddedTwiceInWaitingOnes()
        {
            var taskPool = new TaskPool(1, 100);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));

            var uniqueTask = GetDummyTask();
            Assert.IsTrue(taskPool.Queue(uniqueTask));
            Assert.ThrowsException<ArgumentException>(() => taskPool.Queue(uniqueTask));
        }

        [TestMethod]
        public void Queue_ShouldThrowIfMaxNumberOfRunningTaskIsReached()
        {
            var taskPool = new TaskPool(3, 0);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.ThrowsException<ArgumentException>(() => taskPool.Queue(GetDummyTask()));
        }

        [TestMethod]
        public void Queue_ShouldThrowIfMaxNumberOfWaitingTaskIsReached()
        {
            var taskPool = new TaskPool(2, 2);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.ThrowsException<ArgumentException>(() => taskPool.Queue(GetDummyTask()));
        }

        [TestMethod]
        public void Queue_ShouldThrowIfTaskIsAlreadyRunning()
        {
            var taskPool = new TaskPool(3, 0);
            var task = GetDummyTask();
            Assert.IsNotNull(taskPool);
            task.Start();
            Assert.ThrowsException<ArgumentException>(() => taskPool.Queue(task));
        }

        [TestMethod]
        public void Queue_ShouldThrowIfTaskIsRunnedCanceledOrFaulted()
        {
            var taskPool = new TaskPool(3, 0);
            var task = GetFastDummyTask();
            Assert.IsNotNull(taskPool);
            task.RunSynchronously(); // we do not care here as it should only be a couple milliseconds long
            Assert.ThrowsException<ArgumentException>(() => taskPool.Queue(task));
        }

        [TestMethod]
        public void NumberOfRunningTasks_Test()
        {
            var taskPool = new TaskPool(2, 100);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.AreEqual(taskPool.NumberOfRunningTasks, 2);
        }

        [TestMethod]
        public void NumberOfWaitingTasks_Test()
        {
            var taskPool = new TaskPool(2, 100);
            Assert.IsNotNull(taskPool);
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.IsTrue(taskPool.Queue(GetDummyTask()));
            Assert.AreEqual(taskPool.NumberOfWaitingTasks, 3);
        }
    }
}

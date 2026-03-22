using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ErryLib.ErryRoutines
{
    public class ErryRoutine : IEnumerator
    {
        public ErryRoutine(IEnumerator enumerator)
        {
            this.enumerator = enumerator;
        }

        protected IEnumerator enumerator;
        public object Current => throw new System.NotImplementedException();

        public bool MoveNext()
        {
            return true;
        }

        public void Reset()
        {

        }
        
        async public Task TestMethod()
        {
            await TestMethod();
        }

        public class Instruction
        {

        }
    }

    public class SpecialTask : INotifyCompletion
    {
        private event Action onComplete;
        public bool IsCompleted { get; set; }

        public void OnCompleted(Action continuation) => onComplete += continuation;
        public void GetResult() { }
        public SpecialTask GetAwaiter() => this;
        protected void CompleteTask()
        {
            IsCompleted = true;
            onComplete?.Invoke();
        }

    }
    public class SubTask : SpecialTask
    {
        Func<Task> subTask;
        public SubTask(Func<Task> subTask)
        {
            this.subTask = subTask;
            StartSubTask();
        }

        private async void StartSubTask()
        {
            await subTask.Invoke();
            CompleteTask();
        }

        public static implicit operator SubTask(Func<Task> subTask) => new SubTask(subTask);
    }

    public class EnumeratorTask : SpecialTask, IEnumerator
    {
        private IEnumerator enumerator;
        public EnumeratorTask(IEnumerator enumerator) => this.enumerator = enumerator;


        public object Current => enumerator.Current;
        public bool MoveNext() => enumerator.MoveNext();
        public void Reset() => enumerator.Reset();
    }
}

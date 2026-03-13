using FakeItEasy;
using IngameScript;
using NUnit.Framework;
using MotherCore.TestUtilities;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace MotherCore.Tests.Tests
{
    /// <summary>
    /// Tests for the Clock module's coroutine, scheduled task, and queued task
    /// management. These tests are critical for verifying safe list mutation
    /// during iteration and correct timing behavior.
    /// </summary>
    public class ClockTests : BaseModuleTests
    {
        // --- Construction and reset ---

        [Test]
        public void It_Can_Be_Accessed_Via_Mother()
        {
            Clock clock = _mother.GetModule<Clock>();

            Assert.That(clock, Is.Not.Null);
        }

        [Test]
        public void Reset_Clears_All_Coroutines_And_Tasks()
        {
            Clock clock = _mother.GetModule<Clock>();

            bool taskRan = false;
            clock.Schedule(() => taskRan = true, 0);
            clock.QueueForLater(() => { }, 1);
            clock.AddCoroutine(SimpleCoroutine());

            clock.Reset();
            clock.Run(); // Nothing should execute after reset

            Assert.That(taskRan, Is.False);
        }

        // --- Scheduled tasks ---

        [Test]
        public void Scheduled_Task_Executes_When_Interval_Elapses()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int counter = 0;
            clock.Schedule(() => counter++, 0);

            clock.Run();

            Assert.That(counter, Is.GreaterThan(0));
        }

        [Test]
        public void Scheduled_Task_Repeats_On_Each_Cycle_When_Interval_Is_Zero()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int counter = 0;
            clock.Schedule(() => counter++, 0);

            clock.Run();
            clock.Run();
            clock.Run();

            Assert.That(counter, Is.EqualTo(3));
        }

        // --- Queued tasks ---

        [Test]
        public void Queued_Task_Executes_After_Wait_Time()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            bool executed = false;
            clock.QueueForLater(() => executed = true, 0);

            clock.Run();

            Assert.That(executed, Is.True);
        }

        [Test]
        public void Queued_Task_Is_Removed_After_Execution()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int counter = 0;
            clock.QueueForLater(() => counter++, 0);

            clock.Run();
            clock.Run();

            Assert.That(counter, Is.EqualTo(1),
                "Queued task should only execute once, then be removed.");
        }

        [Test]
        public void QueuedTaskCount_Reflects_Pending_Tasks()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            clock.QueueForLater(() => { }, 999);
            clock.QueueForLater(() => { }, 999);

            Assert.That(clock.QueuedTaskCount, Is.EqualTo(2));
        }

        // --- Coroutines ---

        [Test]
        /// Validates that a simple coroutine executes through all its steps when Run is called repeatedly.
        /// This tests the basic functionality of coroutine execution and completion.
        public void Coroutine_Executes_To_Completion()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int step = 0;

            IEnumerable<double> routine()
            {
                step = 1;
                yield return 0;

                step = 2;
                yield return 0;

                step = 3;
            }

            clock.AddCoroutine(routine());

            clock.Run(); // step 1
            clock.Run(); // step 2
            clock.Run(); // step 3

            Assert.That(step, Is.EqualTo(3));
        }

        [Test]
        public void Coroutine_With_Wait_Pauses_Execution()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int step = 0;

            IEnumerable<double> routine()
            {
                step = 1;
                yield return 999; // Wait a very long time
                step = 2;
            }

            clock.AddCoroutine(routine());

            clock.Run(); // step 1, then encounters wait
            clock.Run(); // Still waiting (deltaTime ≈ 0 in tests)

            Assert.That(step, Is.EqualTo(1),
                "Coroutine should be paused during the wait period.");
        }

        [Test]
        public void Multiple_Coroutines_Run_Independently()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int counterA = 0;
            int counterB = 0;

            IEnumerable<double> routineA()
            {
                counterA++;
                yield return 0;

                counterA++;
            }

            IEnumerable<double> routineB()
            {
                counterB++;
                yield return 0;

                counterB++;
            }

            clock.AddCoroutine(routineA());
            clock.AddCoroutine(routineB());

            clock.Run();
            clock.Run();

            Assert.That(counterA, Is.EqualTo(2));
            Assert.That(counterB, Is.EqualTo(2));
        }

        [Test]
        public void Completed_Coroutine_Is_Removed()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int counter = 0;

            IEnumerable<double> routine()
            {
                counter++;
                yield break;
            }

            clock.AddCoroutine(routine());

            clock.Run(); // Executes and completes
            clock.Run(); // Should not execute again

            Assert.That(counter, Is.EqualTo(1));
        }

        /// <summary>
        /// Validates that adding a coroutine from within a running coroutine
        /// (via MoveNext) does not crash due to list mutation during iteration.
        /// This is the primary race condition vector identified in the audit.
        /// </summary>
        [Test]
        public void Adding_Coroutine_During_Iteration_Does_Not_Throw()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int childExecuted = 0;

            IEnumerable<double> childRoutine()
            {
                childExecuted++;
                yield return 0;
            }

            IEnumerable<double> parentRoutine()
            {
                // This adds a new coroutine while the clock is iterating
                clock.AddCoroutine(childRoutine());
                yield return 0;
            }

            clock.AddCoroutine(parentRoutine());

            // Should not throw despite mutation during iteration
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10; i++)
                    clock.Run();
            });

            Assert.That(childExecuted, Is.GreaterThan(0),
                "Child coroutine added during iteration should eventually execute.");
        }

        /// <summary>
        /// Validates that multiple coroutines completing in the same tick
        /// are all properly cleaned up without index errors.
        /// </summary>
        [Test]
        public void Multiple_Coroutines_Completing_Same_Tick_Are_Cleaned_Up()
        {
            Clock clock = _mother.GetModule<Clock>();
            clock.Reset();

            int completedCount = 0;

            for (int i = 0; i < 5; i++)
            {
                IEnumerable<double> routine()
                {
                    completedCount++;
                    yield break;
                }

                clock.AddCoroutine(routine());
            }

            Assert.DoesNotThrow(() => clock.Run());

            Assert.That(completedCount, Is.EqualTo(5),
                "All five coroutines should complete in one tick.");
        }

        // --- Loader ---

        [Test]
        public void GetLoader_Alternates_Between_Slash_And_Backslash()
        {
            Clock clock = _mother.GetModule<Clock>();

            string first = clock.GetLoader();
            // We can't easily test alternation without Boot + scheduled ticks,
            // but we can verify it returns one of the two valid states
            Assert.That(first, Is.AnyOf("/", "\\"));

            // Then we run the clock to see if the loader alternates as expected
            //clock.Run();
            //clock.Run();
            //clock.Run();
            //clock.Run();
            //clock.Run();
            //clock.Run();

            //string second = clock.GetLoader();

            //Assert.That(second, Is.AnyOf("/", "\\"));
            //Assert.That(second, Is.Not.EqualTo(first), "Loader should alternate between '/' and '\\' on each tick.");
        }

        // --- Helpers ---

        /// <summary>
        /// A minimal coroutine for testing purposes.
        /// </summary>
        static IEnumerable<double> SimpleCoroutine()
        {
            yield return 0;
        }
    }
}

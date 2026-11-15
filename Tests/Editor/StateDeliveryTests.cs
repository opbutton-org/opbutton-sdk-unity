using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace OpButton.SDK
{
    public sealed class StateDeliveryTests
    {
        [Test]
        public void FailedDeliveryRemainsPendingAndAcknowledgesOnlyItsSnapshot()
        {
            var journal = new OpButtonAttributeJournal();
            var values = new Dictionary<string, object> { ["level"] = 1 };
            var first = journal.Snapshot(values);
            Assert.AreEqual(1, Convert.ToInt32(journal.Snapshot(values)["level"]));
            values["level"] = 2;
            journal.Acknowledge(first);
            Assert.AreEqual(2, Convert.ToInt32(journal.Snapshot(values)["level"]));
            journal.Acknowledge(journal.Snapshot(values));
            Assert.IsEmpty(journal.Snapshot(values));
        }

        [Test]
        public void MutableValuesAreDetachedAndNativeTypesSurvive()
        {
            var journal = new OpButtonAttributeJournal();
            var nested = new Dictionary<string, object> { ["level"] = 1 };
            var values = new Dictionary<string, object> { ["state"] = nested, ["optional"] = null, ["enabled"] = false };
            var snapshot = journal.Snapshot(values);
            nested["level"] = 2;
            journal.Acknowledge(snapshot);
            Assert.AreEqual(1, Convert.ToInt32(((Dictionary<string, object>)snapshot["state"])["level"]));
            Assert.IsNull(snapshot["optional"]);
            Assert.IsTrue(journal.Snapshot(values).ContainsKey("state"));
            values["enabled"] = "false";
            Assert.AreEqual("false", journal.Snapshot(values)["enabled"]);
        }

        [Test]
        public void PanelAcknowledgementDoesNotHideNewerEditsOrMoveBackwards()
        {
            var ui = new OpButtonUI();
            var button = ui.AddTab("Controls").Vertical().AddButton("Run");
            int sent = ui.Revision;
            button.Disabled(true);
            ui.MarkSent(sent);
            Assert.IsTrue(ui.HasUnsent());
            ui.MarkSent(ui.Revision);
            ui.MarkSent(sent);
            Assert.IsFalse(ui.HasUnsent());
        }

        [Test]
        public void ProvidersCanDetachDuringSamplingWithoutInvalidatingIteration()
        {
            var ui = new OpButtonUI();
            var stat = ui.AddTab("Controls").Vertical().AddStat("FPS");
            int calls = 0;
            stat.ValueProvider(() =>
            {
                calls++;
                stat.ValueProvider(null);
                return 60;
            });
            ui.PollValues();
            ui.PollValues();
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ClosedSessionRejectsLateSubscriptionsAndCancelsPendingWork()
        {
            var work = new OpButtonSessionWork();
            var cancellation = work.Cancellation;
            work.Dispose();
            Assert.IsTrue(cancellation.IsCancellationRequested);
            Assert.IsFalse(work.ScheduleActions(() => Assert.Fail("Closed session polled"), 1));
            Assert.IsFalse(work.ScheduleMetrics(() => Assert.Fail("Closed session sampled"), 1));
            work.Dispose();
        }
    }
}

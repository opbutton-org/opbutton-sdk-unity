using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace OpButton.SDK
{
    public sealed class PanelLayoutTests
    {
        [Test] public void ReplacingAndRemovingTreesDetachHandlersAndProviders()
        {
            var ui = new OpButtonUI();
            var tab = ui.AddTab("Lifecycle");
            var old = tab.Vertical();
            var slider = old.AddSlider("Speed").OnChangeAcknowledge(action => Assert.Fail("Removed slider dispatched"));
            int samples = 0;
            old.AddStat("FPS").ValueProvider(() => { samples++; return 60; });
            int id = slider.InternalId;
            ui.MarkSent(ui.Revision);
            var grid = tab.Grid();
            Assert.IsTrue(ui.HasUnsent());
            Assert.Greater(grid.InternalId, id);
            ui.PollValues();
            Assert.AreEqual(0, samples);
            Assert.IsFalse(ui.DispatchAction(new OpButtonAction("late", "session", id, OpButtonActionType.Change, OpButtonActionStatus.Delivered, new SimpleJson())));
            var next = grid.AddSlider("Next").OnChangeAcknowledge(action => Assert.Fail("Removed tab dispatched"));
            ui.AddTab("Spacing").Vertical().AddPadding(0);
            ui.RemoveTab(tab.Id);
            Assert.IsFalse(ui.DispatchAction(new OpButtonAction("late", "session", next.InternalId, OpButtonActionType.Change, OpButtonActionStatus.Delivered, new SimpleJson())));
        }
        private static void Collect(Dictionary<string, object> node, HashSet<int> ids)
        {
            Assert.IsTrue(ids.Add(Convert.ToInt32(node["id"])), "Every node has a unique generated ID");
            if (!node.TryGetValue("children", out var children)) return;
            foreach (var child in (List<object>)children) Collect((Dictionary<string, object>)child, ids);
        }

        [Test] public void RetainedLayoutContractAndTypedEvents()
        {
            var ui = new OpButtonUI();
            var root = ui.AddTab("Layout contract").Vertical().Gap(12).Padding(8).Density("compact");
            var row = root.AddHorizontal().Gap(8).Align("end").Justify("space-between").Wrap(true);
            var button = row.AddButton("Apply").Width(140);
            row.AddSelect("Language").Options(new[] { new[] { "en", "English" }, new[] { "ru", "Русский" } })
                .DefaultValue("en").Fill(2).MinWidth(160).MaxWidth(480);
            var radio = root.AddRadioGroup("Difficulty").Options(new[] { new[] { "easy", "Easy" }, new[] { "hard", "Hard" } })
                .DefaultValue("easy").OptionDirection("horizontal");
            var slider = root.AddSlider("Speed").Range(0.5, 3, 0.1).DefaultValue(1.5).Unit("x").Fill();
            root.AddCollapsible("Advanced").Gap(4).Density("comfortable").AddGroup("Group").AddButton("Inside");
            var grid = root.AddGrid().Columns(3).MinCellWidth(140).Gap(8);
            foreach (string text in new[] { "A", "B", "C" }) grid.AddButton(text);
            int identifier = button.InternalId;
            ui.MarkSent(ui.Revision);
            button.Width(160);
            Assert.IsTrue(ui.HasUnsent());
            Assert.AreEqual(identifier, button.InternalId);
            var layout = ui.ToDict();
            var tab = (Dictionary<string, object>)((List<object>)layout["tabs"])[0];
            Collect((Dictionary<string, object>)tab["layout"], new HashSet<int> { Convert.ToInt32(tab["id"]) });
            var collapsed = (Dictionary<string, object>)((List<object>)root.ToDict()["children"])[3];
            var content = (Dictionary<string, object>)((List<object>)collapsed["children"])[0];
            Assert.AreEqual(4, Convert.ToDouble(content["gap"]));
            var numeric = new OpButtonAction("action", "session", slider.InternalId, OpButtonActionType.Change,
                OpButtonActionStatus.Delivered, new SimpleJson(new Dictionary<string, object> { ["value"] = 1.7 }));
            Assert.AreEqual(1.7, new OpButtonSliderAction(numeric).Value);
            var choice = new OpButtonAction("action", "session", radio.InternalId, OpButtonActionType.Change,
                OpButtonActionStatus.Delivered, new SimpleJson(new Dictionary<string, object> { ["value"] = "hard" }));
            Assert.AreEqual("hard", new OpButtonRadioGroupAction(choice).Value);
            string directory = Environment.GetEnvironmentVariable("OPBUTTON_LAYOUT_FIXTURE_DIR");
            if (directory != null) File.WriteAllText(Path.Combine(directory, "unity.json"), ui.ToJson());
        }
    }
}

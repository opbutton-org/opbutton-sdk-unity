using System;
using System.Collections.Generic;

namespace OpButton.SDK
{
    public sealed class OpButtonLayout : OpButtonElement<OpButtonLayout>
    {
        public enum Direction { Vertical, Horizontal, Grid }
        private readonly Direction m_direction;
        private readonly List<IOpButtonLayoutElement> m_children = new List<IOpButtonLayoutElement>();
        private readonly Dictionary<string, object> m_settings = new Dictionary<string, object>();
        internal OpButtonLayout(OpButtonUI ui, Direction direction) : base(ui) { m_direction = direction; }
        public Direction GetDirection() => m_direction;
        private OpButtonLayout Option(string key, object value) { m_settings[key] = value; m_ui.IncrementRevision(); return this; }
        public OpButtonLayout Gap(double pixels) => Option("gap", Dimension(pixels));
        public OpButtonLayout Padding(double pixels) => Option("padding", Dimension(pixels));
        public OpButtonLayout Align(string value) => Option("align", Alignment(value));
        public OpButtonLayout Justify(string value)
        {
            if (value != "start" && value != "center" && value != "end" && value != "space-between") throw new ArgumentException("Invalid justification", nameof(value));
            return Option("justify", value);
        }
        public OpButtonLayout Wrap(bool value) => Option("wrap", value);
        public OpButtonLayout Density(string value)
        {
            if (value != "compact" && value != "comfortable") throw new ArgumentException("Invalid density", nameof(value));
            return Option("density", value);
        }
        public OpButtonLayout Columns(int value)
        {
            if (value < 1 || value > 12) throw new ArgumentOutOfRangeException(nameof(value));
            return Option("gridColumns", value);
        }
        public OpButtonLayout MinCellWidth(double pixels) => Option("minCellWidth", Dimension(pixels, true));
        public OpButtonLayout Title(string value) => Option("title", value);
        private OpButtonLayout AddLayout(Direction direction)
        {
            var child = new OpButtonLayout(m_ui, direction);
            m_children.Add(child); m_ui.IncrementRevision(); return child;
        }
        public OpButtonLayout AddVertical() => AddLayout(Direction.Vertical);
        public OpButtonLayout AddHorizontal() => AddLayout(Direction.Horizontal);
        public OpButtonLayout AddGrid() => AddLayout(Direction.Grid);
        public OpButtonLayout AddGroup(string title) => AddVertical().Title(title);
        public OpButtonLayout AddPadding(double weight)
        {
            m_children.Add(new PaddingElement(m_ui, weight)); m_ui.IncrementRevision(); return this;
        }
        public OpButtonLayout AddCollapsible(string title)
        {
            var child = new CollapsibleElement(m_ui, title);
            m_children.Add(child); m_ui.IncrementRevision(); return child.Content;
        }
        public OpButtonButton AddButton(string label)
        {
            var widget = new OpButtonButton(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonLabel AddLabel(string label)
        {
            var widget = new OpButtonLabel(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonStat AddStat(string label)
        {
            var widget = new OpButtonStat(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonSwitch AddSwitch(string label)
        {
            var widget = new OpButtonSwitch(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonInput AddInput(string label)
        {
            var widget = new OpButtonInput(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonSelect AddSelect(string label)
        {
            var widget = new OpButtonSelect(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonTextarea AddTextarea(string label)
        {
            var widget = new OpButtonTextarea(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonTable AddTable(string label)
        {
            var widget = new OpButtonTable(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonLogs AddLogs(string label)
        {
            var widget = new OpButtonLogs(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonRadioGroup AddRadioGroup(string label)
        {
            var widget = new OpButtonRadioGroup(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        public OpButtonSlider AddSlider(string label)
        {
            var widget = new OpButtonSlider(m_ui, label);
            m_children.Add(widget);
            m_ui.IncrementRevision();
            return widget;
        }

        internal override void Detach() { foreach (var child in m_children) child.Detach(); base.Detach(); }

        internal override Dictionary<string, object> ToDict()
        {
            var json = new Dictionary<string, object>(m_settings) {
                ["type"] = "layout", ["direction"] = m_direction.ToString().ToLowerInvariant()
            };
            var children = new List<object>();
            foreach (var child in m_children) children.Add(child.ToDict());
            json["children"] = children;
            return Decorate(json);
        }
        private sealed class PaddingElement : OpButtonElement<PaddingElement>
        {
            private readonly double m_weight;
            internal PaddingElement(OpButtonUI ui, double weight) : base(ui) { m_weight = weight; }
            internal override Dictionary<string, object> ToDict() => Decorate(new Dictionary<string, object> { ["type"] = "padding", ["weight"] = m_weight });
        }
        private sealed class CollapsibleElement : OpButtonElement<CollapsibleElement>
        {
            private readonly string m_title;
            internal override void Detach() { Content.Detach(); base.Detach(); }
            internal readonly OpButtonLayout Content;
            internal CollapsibleElement(OpButtonUI ui, string title) : base(ui) { m_title = title; Content = new OpButtonLayout(ui, Direction.Vertical); }
            internal override Dictionary<string, object> ToDict() => Decorate(new Dictionary<string, object> {
                ["type"] = "collapsible", ["title"] = m_title, ["children"] = new List<object> { Content.ToDict() }
            });
        }
    }
}

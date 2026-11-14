using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>
    /// A layout container for OpButton UI widgets.
    /// Supports vertical/horizontal arrangement and nesting.
    /// </summary>
    public sealed class OpButtonLayout
    {
        public enum Direction
        {
            Vertical,
            Horizontal
        }

        private readonly OpButtonUI m_ui;
        private readonly Direction m_direction;
        private readonly List<object> m_children = new List<object>();

        internal OpButtonLayout(OpButtonUI ui, Direction direction)
        {
            m_ui = ui;
            m_direction = direction;
        }

        public Direction GetDirection() => m_direction;

        // ── Sub-layouts ──

        public OpButtonLayout AddVertical()
        {
            var sub = new OpButtonLayout(m_ui, Direction.Vertical);
            m_children.Add(sub);
            m_ui.IncrementRevision();
            return sub;
        }

        public OpButtonLayout AddHorizontal()
        {
            var sub = new OpButtonLayout(m_ui, Direction.Horizontal);
            m_children.Add(sub);
            m_ui.IncrementRevision();
            return sub;
        }

        public OpButtonLayout AddCollapsible(string title)
        {
            var collapsible = new CollapsibleElement(m_ui, title);
            m_children.Add(collapsible);
            m_ui.IncrementRevision();
            return collapsible.Content;
        }

        // ── Padding ──

        public OpButtonLayout AddPadding(double weight)
        {
            m_children.Add(new PaddingElement(weight));
            m_ui.IncrementRevision();
            return this;
        }

        // ── Widgets ──

        public OpButtonButton AddButton(string label)
        {
            var w = new OpButtonButton(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public OpButtonLabel AddLabel(string text)
        {
            var w = new OpButtonLabel(m_ui, text);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public OpButtonStat AddStat(string label)
        {
            var w = new OpButtonStat(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public OpButtonSwitch AddSwitch(string label)
        {
            var w = new OpButtonSwitch(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public OpButtonInput AddInput(string label)
        {
            var w = new OpButtonInput(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public OpButtonSelect AddSelect(string label)
        {
            var w = new OpButtonSelect(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public OpButtonTextarea AddTextarea(string label)
        {
            var w = new OpButtonTextarea(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public OpButtonTable AddTable(string label)
        {
            var w = new OpButtonTable(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public OpButtonLogs AddLogs(string label)
        {
            var w = new OpButtonLogs(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        // ── Serialization ──

        internal Dictionary<string, object> ToDict()
        {
            var childrenList = new List<object>();
            foreach (var child in m_children)
            {
                if (child is OpButtonLayout layout)
                    childrenList.Add(layout.ToDict());
                else if (child is PaddingElement padding)
                    childrenList.Add(padding.ToDict());
                else if (child is CollapsibleElement collapsible)
                    childrenList.Add(collapsible.ToDict());
                else
                {
                    // Use reflection-free approach: all widgets derive from OpButtonWidget<T>
                    var method = child.GetType().GetMethod("ToDict",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (method != null)
                        childrenList.Add(method.Invoke(child, null));
                }
            }

            return new Dictionary<string, object>
            {
                ["type"] = "layout",
                ["direction"] = m_direction == Direction.Vertical ? "vertical" : "horizontal",
                ["children"] = childrenList
            };
        }

        // ── Padding helper ──

        private sealed class PaddingElement
        {
            public readonly double Weight;

            public PaddingElement(double weight) { Weight = weight; }

            public Dictionary<string, object> ToDict()
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "padding",
                    ["weight"] = Weight
                };
            }
        }

        // ── Collapsible helper ──

        private sealed class CollapsibleElement
        {
            public readonly string Title;
            public readonly OpButtonLayout Content;

            public CollapsibleElement(OpButtonUI ui, string title)
            {
                Title = title;
                Content = new OpButtonLayout(ui, Direction.Vertical);
            }

            public Dictionary<string, object> ToDict()
            {
                var contentDict = Content.ToDict();
                return new Dictionary<string, object>
                {
                    ["type"] = "collapsible",
                    ["title"] = Title,
                    ["children"] = contentDict.ContainsKey("children") ? contentDict["children"] : new List<object>()
                };
            }
        }
    }
}

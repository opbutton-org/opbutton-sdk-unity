using System;
using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>Inline sizing shared by retained widgets and layouts, in CSS pixels.</summary>
    internal interface IOpButtonLayoutElement
    {
        Dictionary<string, object> ToDict();
        void Detach();
    }

    public abstract class OpButtonElement<T> : IOpButtonLayoutElement where T : OpButtonElement<T>
    {
        protected readonly OpButtonUI m_ui;
        protected readonly int m_internalId;
        private readonly Dictionary<string, object> m_placement = new Dictionary<string, object>();

        internal OpButtonElement(OpButtonUI ui)
        {
            m_ui = ui;
            m_internalId = ui.NextId();
        }

        internal abstract Dictionary<string, object> ToDict();
        Dictionary<string, object> IOpButtonLayoutElement.ToDict() => ToDict();
        void IOpButtonLayoutElement.Detach() => Detach();
        internal virtual void Detach() { m_ui.RegisterCallback(m_internalId, null); }

        public int InternalId => m_internalId;

        internal static double Dimension(double value, bool positive = false)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value > 4096 || (positive ? value <= 0 : value < 0))
                throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }

        internal static string Alignment(string value)
        {
            if (value != "start" && value != "center" && value != "end" && value != "stretch")
                throw new ArgumentException("Invalid layout alignment", nameof(value));
            return value;
        }

        private T Place(string key, object value)
        {
            m_placement[key] = value;
            m_ui.IncrementRevision();
            return (T)this;
        }

        public T Width(double pixels) => Place("width", Dimension(pixels, true));
        public T ContentWidth() => Place("width", "content");
        public T Fill(double weight = 1) { Place("weight", Dimension(weight, true)); return Place("width", "fill"); }
        public T MinWidth(double pixels) => Place("minWidth", Dimension(pixels));
        public T MaxWidth(double pixels) => Place("maxWidth", Dimension(pixels, true));
        public T AlignSelf(string value) => Place("alignSelf", Alignment(value));

        protected Dictionary<string, object> Decorate(Dictionary<string, object> json)
        {
            json["id"] = m_internalId;
            if (m_placement.Count > 0) json["placement"] = new Dictionary<string, object>(m_placement);
            return json;
        }
    }
}

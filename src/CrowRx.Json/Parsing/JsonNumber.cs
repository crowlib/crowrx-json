namespace CrowRx.Json
{
    internal readonly struct JsonNumber
    {
        internal string Text { get; }
        internal bool IsInteger { get; }

        internal JsonNumber(string text, bool isInteger)
        {
            Text = text;
            IsInteger = isInteger;
        }

        public override string ToString() => Text;
    }
}

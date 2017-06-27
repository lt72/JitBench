using System;
using System.IO;

namespace Microsoft.ETWLogAnalyzer.ReportWriters
{
    public class TextReportWriter : IDisposable
    {
        private readonly TextWriter _stream;
        private int _intendationOffset;

        protected TextReportWriter(TextWriter stream)
        {
            _stream = stream;
            _intendationOffset = 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_stream != null)
            {
                _stream.Dispose();
            }
        }

        public virtual void WriteTitle(string text)
        {
            string whiteSpace = new string(' ', 24);
            string line = new string('-', text.Length + 2 * whiteSpace.Length);
            WriteLine(line);
            Write(whiteSpace);
            Write(text);
            WriteLine(whiteSpace);
            WriteLine(line);
        }

        public virtual void AddIndentationLevel(int offset = 1)
        {
            _intendationOffset++;
        }

        public virtual void RemoveIndentationLevel(int offset = 1)
        {
            _intendationOffset -= Math.Min(offset, _intendationOffset);
        }

        public virtual void WriteHeader(string text)
        {
            WriteOffset();
            SkipLine();
            SkipLine();
            WriteLine(text);
            SkipLine();
        }

        public virtual void WriteOffset()
        {
            _stream.Write(new String('\t', _intendationOffset));
        }

        public virtual void WriteFooter(string text)
        {
        }

        public virtual void Write(string text)
        {
            WriteOffset();
            _stream.Write(text);
        }

        public virtual void SkipLine()
        {
            _stream.WriteLine("");
        }

        public virtual void WriteLine(string text)
        {
            WriteOffset();
            _stream.WriteLine(text);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicStore.ETWLogAnalyzer.ReportWriters
{
    internal class TextReportWriter : IDisposable
    {
        private readonly TextWriter _stream;
        private int _intendationOffset;

        protected TextReportWriter(TextWriter stream)
        {
            _stream = stream;
            _intendationOffset = 0;
        }

        public virtual void Dispose()
        {
           _stream.Dispose();
        }

        public virtual void WriteTitle(string text)
        {
            string whiteSpace = new string(' ', 24);
            string line = new string('-', text.Length + 2 * whiteSpace.Length);
            this.WriteLine(line);
            this.Write(whiteSpace);
            this.Write(text);
            this.WriteLine(whiteSpace);
            this.WriteLine(line);
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
            writeOffset();
            this.SkipLine();
            this.SkipLine();
            this.WriteLine(text);
            this.SkipLine();
        }

        public virtual void writeOffset()
        {
            _stream.Write(new String('\t', _intendationOffset));
        }

        public virtual void WriteFooter(string text)
        {
        }

        public virtual void Write(string text)
        {
            writeOffset();
            _stream.Write(text);
        }

        public virtual void SkipLine()
        {
            _stream.WriteLine("");
        }

        public virtual void WriteLine(string text)
        {
            writeOffset();
            _stream.WriteLine(text);
        }
    }
}

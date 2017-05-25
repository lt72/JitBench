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

        protected TextReportWriter(TextWriter stream)
        {
            _stream = stream;
        }

        public virtual void Dispose()
        {
           _stream.Dispose();
        }

        public virtual void WriteHeader(string text)
        {
            this.SkipLine();
            this.SkipLine();
            this.WriteLine(text);
            this.SkipLine();
        }

        public virtual void WriteFooter(string text)
        {
        }

        public virtual void Write(string text)
        {
            _stream.Write(text);
        }

        public virtual void SkipLine()
        {
            _stream.WriteLine("");
        }

        public virtual void WriteLine(string text)
        {
            _stream.WriteLine(text);
        }
    }
}

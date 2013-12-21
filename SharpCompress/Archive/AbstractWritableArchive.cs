﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;

namespace SharpCompress.Archive
{
    public abstract class AbstractWritableArchive<TEntry, TVolume> : AbstractArchive<TEntry, TVolume>
        where TEntry : IArchiveEntry
        where TVolume : IVolume
    {
        private readonly List<TEntry> newEntries = new List<TEntry>();
        private readonly List<TEntry> removedEntries = new List<TEntry>();

        private readonly List<TEntry> modifiedEntries = new List<TEntry>();
        private bool hasModifications;

        internal AbstractWritableArchive(ArchiveType type)
            : base(type)
        {
        }

        internal AbstractWritableArchive(ArchiveType type, Stream stream, Options options)
            : base(type, stream.AsEnumerable(), options)
        {
        }

#if !PORTABLE && !NETFX_CORE
        internal AbstractWritableArchive(ArchiveType type, FileInfo fileInfo, Options options)
            : base(type, fileInfo, options)
        {
        }
#endif

        public override ICollection<TEntry> Entries
        {
            get
            {
                if (hasModifications)
                {
                    return modifiedEntries;
                }
                return base.Entries;
            }
        }

        private void RebuildModifiedCollection()
        {
            hasModifications = true;
            newEntries.RemoveAll(v => removedEntries.Contains(v));
            modifiedEntries.Clear();
            modifiedEntries.AddRange(OldEntries.Concat(newEntries));
        }

        private IEnumerable<TEntry> OldEntries
        {
            get { return base.Entries.Where(x => !removedEntries.Contains(x)); }
        }

        public void RemoveEntry(TEntry entry)
        {
            if (!removedEntries.Contains(entry))
            {
                removedEntries.Add(entry);
                RebuildModifiedCollection();
            }
        }

        public TEntry AddEntry(string filePath, Stream source,
                             long size = 0, DateTime? modified = null)
        {
            return AddEntry(filePath, source, false, size, modified);
        }

        public TEntry AddEntry(string filePath, Stream source, bool closeStream,
                             long size = 0, DateTime? modified = null)
        {
            var entry = CreateEntry(filePath, source, size, modified, closeStream);
            newEntries.Add(entry);
            RebuildModifiedCollection();
            return entry;
        }

#if !PORTABLE && !NETFX_CORE
        public TEntry AddEntry(string filePath, FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                throw new ArgumentException("FileInfo does not exist.");
            }
            return AddEntry(filePath, fileInfo.OpenRead(), true, fileInfo.Length, fileInfo.LastWriteTime);
        }
#endif

        public void SaveTo(Stream stream, CompressionInfo compressionType)
        {
            //reset streams of new entries
            newEntries.Cast<IWritableArchiveEntry>().ForEach(x => x.Stream.Seek(0, SeekOrigin.Begin));
            SaveTo(stream, compressionType, OldEntries, newEntries);
        }

        protected TEntry CreateEntry(string filePath, Stream source, long size, DateTime? modified,
            bool closeStream)
        {
            if (!source.CanRead || !source.CanSeek)
            {
                throw new ArgumentException("Streams must be readable and seekable to use the Writing Archive API");
            }
            //ensure new stream is at the start, this could be reset
            source.Seek(0, SeekOrigin.Begin);
            return CreateEntryInternal(filePath, source, size, modified, closeStream);
        }

        protected abstract TEntry CreateEntryInternal(string filePath, Stream source, long size, DateTime? modified,
                                              bool closeStream);

        protected abstract void SaveTo(Stream stream, CompressionInfo compressionType,
                                       IEnumerable<TEntry> oldEntries, IEnumerable<TEntry> newEntries);

        public override void Dispose()
        {
            base.Dispose();
            newEntries.Cast<Entry>().ForEach(x => x.Close());
            removedEntries.Cast<Entry>().ForEach(x => x.Close());
            modifiedEntries.Cast<Entry>().ForEach(x => x.Close());
        }
    }
}
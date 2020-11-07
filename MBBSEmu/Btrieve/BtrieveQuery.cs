﻿using Microsoft.Data.Sqlite;
using System;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a Btrieve Query that is executed against a given Btrieve File. Remember to
    ///     dispose of this object when it is no longer needed.
    /// </summary>
    public class BtrieveQuery : IDisposable
    {
        /// <summary>
        ///     Wrapper class that contains both a SqliteDataReader and its associated SqliteCommand
        ///     so that you can close both via Dispose.
        /// </summary>
        public class SqliteReader : IDisposable
        {
            public SqliteCommand Command { get; set; }
            public SqliteDataReader DataReader { get; set; }

            public bool Read() => DataReader.Read();

            public void Dispose()
            {
                DataReader?.Dispose();
                Command?.Dispose();
            }
        }

        public enum CursorDirection {
            Forward,
            Reverse
        }

        public SqliteConnection Connection { get; set; }

        public CursorDirection Direction { get; set; }

        /// <summary>
        ///     Initial Key Value to be queried on
        /// </summary>
        public byte[] KeyData { get; set; }

        /// <summary>
        ///     Last Key Value retrieved during GetNext/GetPrevious cursor movement,
        ///     as a Sqlite object.
        /// </summary>
        public object LastKey { get; set; }

        /// <summary>
        ///     Key Definition
        /// </summary>
        public BtrieveKey Key { get; set; }

        /// <summary>
        ///     Current position of the query. Changes as GetNext/GetPrevious is called
        /// </summary>
        /// <value></value>
        public uint Position { get; set; }

        private SqliteReader _reader;
        public SqliteReader Reader {
            get { return _reader; }
            set
            {
                if (_reader != value)
                {
                    _reader?.Dispose();
                    _reader = value;

                    Connection = value?.Command?.Connection ?? Connection;
                }
            }
        }

        public BtrieveQuery()
        {
            Position = 0;
            Direction = CursorDirection.Forward;
        }

        public void Dispose()
        {
            Reader = null;
        }

        /// <summary>
        ///     A delegate function that returns true if the retrieved record matches the query.
        /// </summary>
        /// <param name="query">Query made</param>
        /// <param name="record">The record retrieve from the query</param>
        /// <returns>true if the record is valid for the query</returns>
        public delegate bool QueryMatcher(BtrieveQuery query, BtrieveRecord record);

        private void SeekTo(uint position)
        {
            while (Reader.Read())
            {
                var cursorPosition = (uint)Reader.DataReader.GetInt32(0);
                if (cursorPosition == position)
                    return;
            }

            // at end, nothing left
            Reader = null;
        }

        private void ChangeDirection(CursorDirection newDirection)
        {
            if (LastKey == null) // no successful prior query, so abort
                return;

            var command = new SqliteCommand() { Connection = this.Connection };
            command.CommandText = $"SELECT id, {Key.SqliteKeyName}, data FROM data_t WHERE {Key.SqliteKeyName} ";
            switch (newDirection)
            {
                case CursorDirection.Forward:
                    command.CommandText += $">= @value ORDER BY {Key.SqliteKeyName} ASC";
                    break;
                case CursorDirection.Reverse:
                    command.CommandText += $"<= @value ORDER BY {Key.SqliteKeyName} DESC";
                    break;
                default:
                    throw new ArgumentException("Bad direction");
            }

            command.Parameters.AddWithValue("@value", LastKey);

            Reader = new BtrieveQuery.SqliteReader()
            {
                DataReader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo),
                Command = command
            };
            Direction = newDirection;
            // due to duplicate keys, we need to seek past the current position since we might serve
            // data already served
            SeekTo(Position);
        }

        /// <summary>
        ///     Updates Position based on the value of current Sqlite cursor.
        ///
        ///     <para/>If the query has ended, it invokes query.ContinuationReader to get the next
        ///     Sqlite cursor and continues from there.
        /// </summary>
        /// <param name="query">Current query</param>
        /// <param name="matcher">Delegate function for verifying results. If this matcher returns
        ///     false, the query is aborted and returns no more results.</param>
        /// <returns>true if the Sqlite cursor returned a valid item along with the data</returns>
        public (bool, BtrieveRecord) Next(QueryMatcher matcher, CursorDirection cursorDirection)
        {
            if (Direction != cursorDirection)
            {
                Reader = null;
                ChangeDirection(cursorDirection);
            }

            // out of records?
            if (Reader == null || !Reader.Read())
            {
                Reader = null;
                return (false, null);
            }

            Position = (uint)Reader.DataReader.GetInt32(0);
            LastKey = Reader.DataReader.GetValue(1);

            using var stream = Reader.DataReader.GetStream(2);
            var data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);

            var record = new BtrieveRecord(Position, data);

            if (!matcher.Invoke(this, record))
                return (false, record);

            return (true, record);
        }
    }
}

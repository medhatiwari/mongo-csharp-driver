/* Copyright 2013-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver.Core.Misc;
using MongoDB.Shared;

namespace MongoDB.Driver
{
    /// <summary>
    /// Represents a read preference.
    /// </summary>
    public sealed class ReadPreference : IEquatable<ReadPreference>
    {
        #region static
        // static fields
        private static readonly TagSet[] __emptyTagSetsArray = new TagSet[0];
        private static readonly ReadPreference __nearest = new ReadPreference(ReadPreferenceMode.Nearest);
        private static readonly ReadPreference __primary = new ReadPreference(ReadPreferenceMode.Primary);
        private static readonly ReadPreference __primaryPreferred = new ReadPreference(ReadPreferenceMode.PrimaryPreferred);
        private static readonly ReadPreference __secondary = new ReadPreference(ReadPreferenceMode.Secondary);
        private static readonly ReadPreference __secondaryPreferred = new ReadPreference(ReadPreferenceMode.SecondaryPreferred);

        // static properties
        /// <summary>
        /// Gets an instance of ReadPreference that represents a Nearest read preference.
        /// </summary>
        /// <value>
        /// An instance of ReadPreference that represents a Nearest read preference.
        /// </value>
        public static ReadPreference Nearest
        {
            get { return __nearest; }
        }

        /// <summary>
        /// Gets an instance of ReadPreference that represents a Primary read preference.
        /// </summary>
        /// <value>
        /// An instance of ReadPreference that represents a Primary read preference.
        /// </value>
        public static ReadPreference Primary
        {
            get { return __primary; }
        }

        /// <summary>
        /// Gets an instance of ReadPreference that represents a PrimaryPreferred read preference.
        /// </summary>
        /// <value>
        /// An instance of ReadPreference that represents a PrimaryPreferred read preference.
        /// </value>
        public static ReadPreference PrimaryPreferred
        {
            get { return __primaryPreferred; }
        }

        /// <summary>
        /// Gets an instance of ReadPreference that represents a Secondary read preference.
        /// </summary>
        /// <value>
        /// An instance of ReadPreference that represents a Secondary read preference.
        /// </value>
        public static ReadPreference Secondary
        {
            get { return __secondary; }
        }

        /// <summary>
        /// Gets an instance of ReadPreference that represents a SecondaryPreferred read preference.
        /// </summary>
        /// <value>
        /// An instance of ReadPreference that represents a SecondaryPreferred read preference.
        /// </value>
        public static ReadPreference SecondaryPreferred
        {
            get { return __secondaryPreferred; }
        }

        // public static methods
        /// <summary>
        /// Creates a new ReadPreference instance from a BsonDocument.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>A ReadPreference.</returns>
        public static ReadPreference FromBsonDocument(BsonDocument document)
        {
            ReadPreferenceMode mode = ReadPreferenceMode.Primary;
            TimeSpan? maxStaleness = null;
            List<TagSet> tagSets = null;
#pragma warning disable CS0618 // Type or member is obsolete
            ReadPreferenceHedge hedge = null;
#pragma warning restore CS0618 // Type or member is obsolete

            foreach (var element in document)
            {
                switch (element.Name)
                {
                    case "mode":
                        mode = (ReadPreferenceMode)Enum.Parse(typeof(ReadPreferenceMode), element.Value.AsString, ignoreCase: true);
                        break;

                    case "tags":
                    case "tagSets":
                        tagSets = new List<TagSet>();
                        foreach (var tagsDocument in element.Value.AsBsonArray.Cast<BsonDocument>())
                        {
                            var tags = new List<Tag>();
                            foreach (var tagElement in tagsDocument)
                            {
                                var tag = new Tag(tagElement.Name, tagElement.Value.AsString);
                                tags.Add(tag);
                            }
                            var tagSet = new TagSet(tags);
                            tagSets.Add(tagSet);
                        }
                        break;

                    case "maxStaleness":
                        maxStaleness = element.Value.BsonType switch
                        {
                            BsonType.String => TimeSpanParser.Parse(element.Value.AsString),
                            _ => TimeSpan.FromSeconds(element.Value.ToDouble())
                        };
                        break;

                    case "maxStalenessSeconds":
                        maxStaleness = TimeSpan.FromSeconds(element.Value.ToDouble());
                        break;

                    case "hedge":
                        var hedgeEnabled = element.Value.AsBsonDocument["enabled"].AsBoolean;
#pragma warning disable CS0618 // Type or member is obsolete
                        hedge = new ReadPreferenceHedge(hedgeEnabled);
#pragma warning restore CS0618 // Type or member is obsolete
                        break;

                    default:
                        throw new ArgumentException($"Invalid element in ReadPreference document: {element.Name}.");
                }
            }

            return new ReadPreference(mode, tagSets, maxStaleness, hedge);
        }
        #endregion

        // fields
#pragma warning disable CS0618 // Type or member is obsolete
        private readonly ReadPreferenceHedge _hedge;
#pragma warning restore CS0618 // Type or member is obsolete
        private readonly TimeSpan? _maxStaleness;
        private readonly ReadPreferenceMode _mode;
        private readonly IReadOnlyList<TagSet> _tagSets;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="ReadPreference" /> class.
        /// </summary>
        /// <param name="mode">The read preference mode.</param>
        /// <param name="tagSets">The tag sets.</param>
        /// <param name="maxStaleness">The maximum staleness.</param>
        /// <param name="hedge">The hedge.</param>
        public ReadPreference(
            ReadPreferenceMode mode,
            IEnumerable<TagSet> tagSets = null,
            TimeSpan? maxStaleness = null,
#pragma warning disable CS0618 // Type or member is obsolete
            ReadPreferenceHedge hedge = null)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            var tagSetsArray = tagSets == null ? __emptyTagSetsArray : tagSets.ToArray();
            if (tagSetsArray.Length > 0)
            {
                Ensure.That(mode != ReadPreferenceMode.Primary, "TagSets cannot be used with ReadPreferenceMode Primary.", nameof(tagSets));
            }

            if (maxStaleness.HasValue)
            {
                Ensure.IsInfiniteOrGreaterThanZero(maxStaleness.Value, nameof(maxStaleness));
                if (maxStaleness.Value > TimeSpan.Zero)
                {
                    Ensure.That(maxStaleness.Value.Ticks % TimeSpan.TicksPerMillisecond == 0, "MaxStaleness must not have fractional seconds.", nameof(maxStaleness));
                }
                Ensure.That(mode != ReadPreferenceMode.Primary, "MaxStaleness cannot be used with ReadPreferenceMode Primary.", nameof(maxStaleness));
            }

            if (hedge != null)
            {
                Ensure.That(mode != ReadPreferenceMode.Primary, "Hedged reads cannot be used with ReadPreferenceMode Primary.", nameof(hedge));
            }

            _mode = mode;
            _tagSets = tagSetsArray;
            _maxStaleness = maxStaleness;
            _hedge = hedge;
        }

        // properties
        /// <summary>
        /// Gets the hedge.
        /// </summary>
        [Obsolete("Hedged reads are deprecated starting in server version 8.0.")]
        public ReadPreferenceHedge Hedge => _hedge;

        /// <summary>
        /// Gets the maximum staleness.
        /// </summary>
        /// <value>
        /// The maximum staleness.
        /// </value>
        public TimeSpan? MaxStaleness
        {
            get { return _maxStaleness; }
        }

        /// <summary>
        /// Gets the read preference mode.
        /// </summary>
        /// <value>
        /// The read preference mode.
        /// </value>
        public ReadPreferenceMode ReadPreferenceMode
        {
            get { return _mode; }
        }

        /// <summary>
        /// Gets the tag sets.
        /// </summary>
        /// <value>
        /// The tag sets.
        /// </value>
        public IReadOnlyList<TagSet> TagSets
        {
            get { return _tagSets; }
        }

        // methods
        /// <inheritdoc/>
        public bool Equals(ReadPreference other)
        {
            if (other == null)
            {
                return false;
            }

            return
                object.Equals(_hedge, other._hedge) &&
                _maxStaleness.Equals(other._maxStaleness) &&
                _mode.Equals(other._mode) &&
                _tagSets.SequenceEqual(other.TagSets);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as ReadPreference);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return new Hasher()
                .Hash(_hedge)
                .Hash(_maxStaleness)
                .Hash(_mode)
                .HashElements(_tagSets)
                .GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("{ Mode : ");
            sb.Append(_mode.ToString());
            if (_tagSets.Count > 0)
            {
                sb.Append(", TagSets : [");
                sb.Append(string.Join(", ", _tagSets));
                sb.Append("]");
            }
            if (_maxStaleness.HasValue)
            {
                sb.Append(", MaxStaleness : ");
                sb.Append(TimeSpanParser.ToString(_maxStaleness.Value));
            }
            if (_hedge != null)
            {
                sb.Append(", Hedge : ");
                sb.Append(_hedge.ToBsonDocument().ToJson());
            }
            sb.Append(" }");
            return sb.ToString();
        }

        /// <summary>
        /// Returns a new instance of ReadPreference with some values changed.
        /// </summary>
        /// <param name="hedge">The hedge.</param>
        /// <returns>A new instance of ReadPreference.</returns>
        [Obsolete("Hedged reads are deprecated starting in server version 8.0.")]
        public ReadPreference With(ReadPreferenceHedge hedge)
        {
            return new ReadPreference(_mode, _tagSets, _maxStaleness, hedge);
        }

        /// <summary>
        /// Returns a new instance of ReadPreference with some values changed.
        /// </summary>
        /// <param name="mode">The read preference mode.</param>
        /// <returns>A new instance of ReadPreference.</returns>
        public ReadPreference With(ReadPreferenceMode mode)
        {
            return new ReadPreference(mode, _tagSets, _maxStaleness, _hedge);
        }

        /// <summary>
        /// Returns a new instance of ReadPreference with some values changed.
        /// </summary>
        /// <param name="tagSets">The tag sets.</param>
        /// <returns>A new instance of ReadPreference.</returns>
        public ReadPreference With(IEnumerable<TagSet> tagSets)
        {
            return new ReadPreference(_mode, tagSets, _maxStaleness, _hedge);
        }

        /// <summary>
        /// Returns a new instance of ReadPreference with some values changed.
        /// </summary>
        /// <param name="maxStaleness">The maximum staleness.</param>
        /// <returns>A new instance of ReadPreference.</returns>
        public ReadPreference With(TimeSpan? maxStaleness)
        {
            return new ReadPreference(_mode, _tagSets, maxStaleness, _hedge);
        }
    }
}

//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Text.Json;
using OrganismBase;
using Terrarium.Game;
using Terrarium.Hosting;

namespace Terrarium.Persistence
{
    /// <summary>
    ///  Reads and writes saved terrariums (.ter files).  A save is a JSON
    ///  document with the world settings, the current world vector and the
    ///  scheduler's organisms.
    /// </summary>
    internal static class SaveFile
    {
        /// <summary>
        ///  Identifies a Terrarium save file.
        /// </summary>
        internal const string FormatName = "Terrarium.Save";

        /// <summary>
        ///  The version of the format this build writes.
        /// </summary>
        internal const int FormatVersion = 1;

        /// <summary>
        ///  Everything a save file holds.
        /// </summary>
        internal sealed class Contents
        {
            public int WorldWidth;
            public int WorldHeight;
            public int MaxPlants;
            public int MaxAnimals;
            public int PlantCount;
            public int AnimalCount;
            public WorldVector Vector;
            public GameObjectCollection Organisms;
        }

        private static GraphSerializer CreateSerializer()
        {
            var serializer = new GraphSerializer(typeof (GameEngine).Assembly, typeof (OrganismState).Assembly);
            serializer.AddRecordFactory(typeof (OrganismWrapper), OrganismWrapper.FromSaveValues);
            return serializer;
        }

        /// <summary>
        ///  Writes a save file.  The file is written next to the target first
        ///  and then swapped in, so a failed save leaves the old file intact.
        /// </summary>
        internal static void Write(string path, Contents contents)
        {
            var serializer = CreateSerializer();
            var temporaryPath = path + ".saving";

            using (var stream = new FileStream(temporaryPath, FileMode.Create))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions {MaxDepth = 256}))
            {
                writer.WriteStartObject();
                writer.WriteString("format", FormatName);
                writer.WriteNumber("version", FormatVersion);
                writer.WriteNumber("worldWidth", contents.WorldWidth);
                writer.WriteNumber("worldHeight", contents.WorldHeight);
                writer.WriteNumber("maxPlants", contents.MaxPlants);
                writer.WriteNumber("maxAnimals", contents.MaxAnimals);
                writer.WriteNumber("plantCount", contents.PlantCount);
                writer.WriteNumber("animalCount", contents.AnimalCount);
                writer.WritePropertyName("vector");
                serializer.Write(writer, contents.Vector, typeof (WorldVector));
                writer.WritePropertyName("organisms");
                serializer.Write(writer, contents.Organisms, typeof (GameObjectCollection));
                writer.WriteEndObject();
            }

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        /// <summary>
        ///  Reads a save file.
        /// </summary>
        /// <exception cref="OldSaveFileException">The file was saved by an older version of Terrarium.</exception>
        internal static Contents Read(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                // Old saves are BinaryFormatter streams, which never start with '{'
                int first;
                do
                {
                    first = stream.ReadByte();
                } while (first == ' ' || first == '\t' || first == '\r' || first == '\n' || first == 0xEF ||
                         first == 0xBB || first == 0xBF);

                if (first != '{')
                {
                    throw new OldSaveFileException(path);
                }

                stream.Position = 0;
                using (var document = JsonDocument.Parse(stream, new JsonDocumentOptions {MaxDepth = 256}))
                {
                    var root = document.RootElement;
                    JsonElement format;
                    if (!root.TryGetProperty("format", out format) || format.GetString() != FormatName)
                    {
                        throw new GameEngineException("'" + path + "' isn't a Terrarium save file.");
                    }

                    var version = root.GetProperty("version").GetInt32();
                    if (version > FormatVersion)
                    {
                        throw new GameEngineException("'" + path +
                                                      "' was saved by a newer version of Terrarium.");
                    }

                    var serializer = CreateSerializer();
                    return new Contents
                               {
                                   WorldWidth = root.GetProperty("worldWidth").GetInt32(),
                                   WorldHeight = root.GetProperty("worldHeight").GetInt32(),
                                   MaxPlants = root.GetProperty("maxPlants").GetInt32(),
                                   MaxAnimals = root.GetProperty("maxAnimals").GetInt32(),
                                   PlantCount = root.GetProperty("plantCount").GetInt32(),
                                   AnimalCount = root.GetProperty("animalCount").GetInt32(),
                                   Vector = (WorldVector) serializer.Read(root.GetProperty("vector"),
                                                                          typeof (WorldVector)),
                                   Organisms = (GameObjectCollection) serializer.Read(root.GetProperty("organisms"),
                                                                                      typeof (GameObjectCollection))
                               };
                }
            }
        }
    }
}

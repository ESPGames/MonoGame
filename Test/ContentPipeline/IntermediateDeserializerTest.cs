﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Xml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
#if XNA
using System.Reflection;
#endif

namespace MonoGame.Tests.ContentPipeline
{
    // These tests are based on "Everything you ever wanted to know about IntermediateSerializer" by Shawn Hargreaves
    // http://blogs.msdn.com/b/shawnhar/archive/2008/08/12/everything-you-ever-wanted-to-know-about-intermediateserializer.aspx

    class IntermediateDeserializerTest
    {
        class TestContentManager : ContentManager
        {
            class FakeGraphicsService : IGraphicsDeviceService
            {
                public GraphicsDevice GraphicsDevice { get; private set; }
                public event EventHandler<EventArgs> DeviceCreated;
                public event EventHandler<EventArgs> DeviceDisposing;
                public event EventHandler<EventArgs> DeviceReset;
                public event EventHandler<EventArgs> DeviceResetting;
            }

            class FakeServiceProvider : IServiceProvider
            {
                public object GetService(Type serviceType)
                {
                    if (serviceType == typeof(IGraphicsDeviceService))
                        return new FakeGraphicsService();

                    throw new NotImplementedException();
                }
            }

            private readonly MemoryStream _xnbStream;

            public TestContentManager(MemoryStream xnbStream)
                : base(new FakeServiceProvider(), "NONE")
            {
                _xnbStream = xnbStream;
            }

            protected override Stream OpenStream(string assetName)
            {
                return new MemoryStream(_xnbStream.GetBuffer(), false);
            }
        }

        private static T Deserialize<T>(string file, Action<T> doAsserts)
        {
            object result;
            var filePath = Paths.Xml(file);
            using (var reader = XmlReader.Create(filePath))
                result = IntermediateSerializer.Deserialize<object>(reader, filePath);

            Assert.NotNull(result);
            Assert.IsAssignableFrom<T>(result);

            doAsserts((T)result);

            return (T)result;
        }

        private static void DeserializeCompileAndLoad<T>(string file, Action<T> doAsserts)
        {
            var result = Deserialize(file, doAsserts);

            var xnbStream = new MemoryStream();
#if XNA
            // In MS XNA the ContentCompiler is completely internal, so we need
            // to use just a little reflection to get access to what we need.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var ctor = typeof(ContentCompiler).GetConstructors(flags)[0];
            var compiler = (ContentCompiler)ctor.Invoke(null);
            var compileMethod = typeof(ContentCompiler).GetMethod("Compile", flags);
            compileMethod.Invoke(compiler, new object[] { xnbStream, result, TargetPlatform.Windows, GraphicsProfile.Reach,
                                false, Directory.GetCurrentDirectory(), "referenceRelocationPath" });
#else
            var compiler = new ContentCompiler();
            compiler.Compile(xnbStream, result, TargetPlatform.Windows, GraphicsProfile.Reach, 
                                false, "rootDirectory", "referenceRelocationPath");
#endif

            var content = new TestContentManager(xnbStream);
            var loaded = content.Load<T>("Whatever");

            doAsserts(loaded);
        }

        [Test]
        public void TheBasics()
        {
            DeserializeCompileAndLoad<TheBasics>("01_TheBasics.xml", theBasics =>
            {
                Assert.AreEqual(1, theBasics.PublicField);
                Assert.AreEqual(0, theBasics.InternalField);
                Assert.AreEqual("Hello World", theBasics.GetSetProperty);
                Assert.NotNull(theBasics.Nested);
                Assert.AreEqual("Shawn", theBasics.Nested.Name);
                Assert.AreEqual(true, theBasics.Nested.IsEnglish);
            });
        }

        [Test]
        public void Inheritance()
        {
            DeserializeCompileAndLoad<Inheritance>("02_Inheritance.xml", inheritance =>
            {
                Assert.AreEqual(23, inheritance.elf);
                Assert.AreEqual("world", inheritance.hello);
            });
        }

        [Test]
        public void IncludingPrivateMembers()
        {
            DeserializeCompileAndLoad<IncludingPrivateMembers>("03_IncludingPrivateMembers.xml", including =>
            {
                Assert.AreEqual(23, including.GetElfValue());
            });
        }

        [Test]
        public void ExcludingPublicMembers()
        {
            var filePath = Paths.Xml("04_ExcludingPublicMembers.xml");
            using (var reader = XmlReader.Create(filePath))
            {
                // This should throw an InvalidContentException as the
                // xml tries to set the <elf> element which has a 
                // [ContentSerializerIgnore] attribute.
                Assert.Throws<InvalidContentException>(() =>
                    IntermediateSerializer.Deserialize<object>(reader, filePath));
            }
        }

        [Test]
        public void RenamingXmlElements()
        {
            DeserializeCompileAndLoad<RenamingXmlElements>("05_RenamingXmlElements.xml", renaming =>
            {
                Assert.AreEqual("world", renaming.hello);
                Assert.AreEqual(23, renaming.elf);
            });
        }

        [Test]
        public void NullReferences()
        {
            DeserializeCompileAndLoad<NullReferences>("06_NullReferences.xml", nullref =>
            {
                Assert.AreEqual(null, nullref.hello);
            });
        }

        [Test]
        public void OptionalElements()
        {
            DeserializeCompileAndLoad<OptionalElements>("07_OptionalElements.xml", optional =>
            {
                Assert.AreEqual(null, optional.a);
                Assert.AreEqual(null, optional.b);
                Assert.AreEqual(string.Empty, optional.c);
            });
        }

        [Test]
        public void AllowNull()
        {
            var filePath = Paths.Xml("08_AllowNull.xml");
            using (var reader = XmlReader.Create(filePath))
            {
                // This should throw an InvalidContentException as the
                // xml tries to set the <elf> element which has a 
                // [ContentSerializerIgnore] attribute.
                Assert.Throws<InvalidContentException>(() =>
                    IntermediateSerializer.Deserialize<object>(reader, filePath));
            }
        }

        [Test]
        public void Collections()
        {
            DeserializeCompileAndLoad<Collections>("09_Collections.xml", collections =>
            {
                Assert.NotNull(collections.StringArray);
                Assert.AreEqual(2, collections.StringArray.Length);
                Assert.AreEqual("Hello", collections.StringArray[0]);
                Assert.AreEqual("World", collections.StringArray[1]);

                Assert.NotNull(collections.StringList);
                Assert.AreEqual(4, collections.StringList.Count);
                Assert.AreEqual("This", collections.StringList[0]);
                Assert.AreEqual("is", collections.StringList[1]);
                Assert.AreEqual("a", collections.StringList[2]);
                Assert.AreEqual("test", collections.StringList[3]);

                Assert.NotNull(collections.IntArray);
                Assert.AreEqual(5, collections.IntArray.Length);
                Assert.AreEqual(1, collections.IntArray[0]);
                Assert.AreEqual(2, collections.IntArray[1]);
                Assert.AreEqual(3, collections.IntArray[2]);
                Assert.AreEqual(23, collections.IntArray[3]);
                Assert.AreEqual(42, collections.IntArray[4]);
            });            
        }

        [Test]
        public void CollectionItemName()
        {
            DeserializeCompileAndLoad<CollectionItemName>("10_CollectionItemName.xml", collections =>
            {
                Assert.NotNull(collections.StringArray);
                Assert.AreEqual(2, collections.StringArray.Length);
                Assert.AreEqual("Hello", collections.StringArray[0]);
                Assert.AreEqual("World", collections.StringArray[1]);
            });
        }

        [Test]
        public void Dictionaries()
        {
            DeserializeCompileAndLoad<Dictionaries>("11_Dictionaries.xml", dictionaries =>
            {
                Assert.NotNull(dictionaries.TestDictionary);
                Assert.AreEqual(2, dictionaries.TestDictionary.Count);
                Assert.AreEqual(true, dictionaries.TestDictionary[23]);
                Assert.AreEqual(false, dictionaries.TestDictionary[42]);
            });
        }

        [Test]
        public void MathTypes()
        {
            DeserializeCompileAndLoad<MathTypes>("12_MathTypes.xml", mathTypes =>
            {
                Assert.AreEqual(new Point(1, 2), mathTypes.Point);
                Assert.AreEqual(new Rectangle(1, 2, 3, 4), mathTypes.Rectangle);
                Assert.AreEqual(new Vector3(1, 2, 3), mathTypes.Vector3);
                Assert.AreEqual(new Vector4(1, 2, 3, 4), mathTypes.Vector4);
                Assert.AreEqual(new Quaternion(1, 2, 3, 4), mathTypes.Quaternion);
                Assert.AreEqual(new Plane(1, 2, 3, 4), mathTypes.Plane);
                Assert.AreEqual(new Matrix(1, 2, 3, 4, 5 , 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16), mathTypes.Matrix);
                Assert.AreEqual(Color.CornflowerBlue, mathTypes.Color);
                Assert.NotNull(mathTypes.Vector2Array);
                Assert.AreEqual(2, mathTypes.Vector2Array.Length);
                Assert.AreEqual(Vector2.Zero, mathTypes.Vector2Array[0]);
                Assert.AreEqual(Vector2.One, mathTypes.Vector2Array[1]);
            });
        }

        [Test]
        public void PrimitiveTypes()
        {
            DeserializeCompileAndLoad<PrimitiveTypes>("18_PrimitiveTypes.xml", primitiveTypes =>
            {
                Assert.AreEqual('A', primitiveTypes.Char);
                Assert.AreEqual(127, primitiveTypes.Byte);
                Assert.AreEqual(-127, primitiveTypes.SByte);
                Assert.AreEqual(-1000, primitiveTypes.Short);
                Assert.AreEqual(1000, primitiveTypes.UShort);
                Assert.AreEqual(-100000, primitiveTypes.Int);
                Assert.AreEqual(100000, primitiveTypes.UInt);
                Assert.AreEqual(-10000000, primitiveTypes.Long);
                Assert.AreEqual(10000000, primitiveTypes.ULong);
                Assert.AreEqual(1234567.0f, primitiveTypes.Float);
                Assert.AreEqual(1234567890.0, primitiveTypes.Double);
                Assert.AreEqual(null, primitiveTypes.NullChar);
                Assert.AreEqual(' ', primitiveTypes.NotNullChar);
            });
        }

        [Test]
        public void PolymorphicTypes()
        {
            DeserializeCompileAndLoad<PolymorphicTypes>("13_PolymorphicTypes.xml", polymorphicTypes =>
            {
                Assert.AreEqual("World", polymorphicTypes.Hello);
                Assert.AreEqual(23, polymorphicTypes.Elf);

                Assert.NotNull(polymorphicTypes.TypedArray);
                Assert.AreEqual(3, polymorphicTypes.TypedArray.Length);
                Assert.IsAssignableFrom<PolymorphicA>(polymorphicTypes.TypedArray[0]);
                Assert.AreEqual(true, polymorphicTypes.TypedArray[0].Value);
                Assert.IsAssignableFrom<PolymorphicB>(polymorphicTypes.TypedArray[1]);
                Assert.AreEqual(true, polymorphicTypes.TypedArray[1].Value);
                Assert.IsAssignableFrom<PolymorphicC>(polymorphicTypes.TypedArray[2]);
                Assert.AreEqual(true, polymorphicTypes.TypedArray[2].Value);

                Assert.NotNull(polymorphicTypes.UntypedArray);
                Assert.AreEqual(3, polymorphicTypes.UntypedArray.Length);
                Assert.IsAssignableFrom<PolymorphicA>(polymorphicTypes.UntypedArray.GetValue(0));
                Assert.AreEqual(true, ((PolymorphicA)polymorphicTypes.UntypedArray.GetValue(0)).Value);
                Assert.IsAssignableFrom<PolymorphicB>(polymorphicTypes.UntypedArray.GetValue(1));
                Assert.AreEqual(true, ((PolymorphicB)polymorphicTypes.UntypedArray.GetValue(1)).Value);
                Assert.IsAssignableFrom<PolymorphicC>(polymorphicTypes.UntypedArray.GetValue(2));
                Assert.AreEqual(true, ((PolymorphicC)polymorphicTypes.UntypedArray.GetValue(2)).Value);
            });
        }

        [Test]
        public void Namespaces()
        {
            DeserializeCompileAndLoad<NamespaceClass>("14_Namespaces.xml", namespaceClass =>
            {
                Assert.IsAssignableFrom<NamespaceHelper>(namespaceClass.A);
                Assert.AreEqual(true, ((NamespaceHelper)namespaceClass.A).Value);
                Assert.IsAssignableFrom<Vector2>(namespaceClass.B);
                Assert.AreEqual(Vector2.Zero, namespaceClass.B);
                Assert.IsAssignableFrom<SpriteSortMode>(namespaceClass.C);
                Assert.AreEqual(SpriteSortMode.Immediate, namespaceClass.C);
            });
        }

        [Test]
        public void FlattenContent()
        {
            DeserializeCompileAndLoad<FlattenContent>("15_FlattenContent.xml", flattenContent =>
            {
                Assert.IsAssignableFrom<NestedClass>(flattenContent.Nested);
                Assert.NotNull(flattenContent.Nested);
                Assert.AreEqual("Shawn", flattenContent.Nested.Name);
                Assert.AreEqual(true, flattenContent.Nested.IsEnglish);
                Assert.NotNull(flattenContent.Collection);
                Assert.AreEqual(2, flattenContent.Collection.Length);
                Assert.AreEqual("Hello", flattenContent.Collection[0]);
                Assert.AreEqual("World", flattenContent.Collection[1]);
            });
        }

        [Test]
        public void SharedResources()
        {
            DeserializeCompileAndLoad<SharedResources>("16_SharedResources.xml", sharedResources =>
            {
                Assert.NotNull(sharedResources.Head);
                Assert.AreEqual(1, sharedResources.Head.Value);
                Assert.NotNull(sharedResources.Head.Next);
                Assert.AreEqual(2, sharedResources.Head.Next.Value);
                Assert.NotNull(sharedResources.Head.Next.Next);
                Assert.AreEqual(3, sharedResources.Head.Next.Next.Value);
                Assert.AreSame(sharedResources.Head, sharedResources.Head.Next.Next.Next);
            });
        }

        [Test]
        public void ExternalReferences()
        {
            Deserialize<ExternalReferences>("17_ExternalReferences.xml", externalReferences =>
            {
                Assert.NotNull(externalReferences.Texture);
                Assert.IsTrue(externalReferences.Texture.Filename.EndsWith(@"\Xml\grass.tga"));
                Assert.NotNull(externalReferences.Shader);
                Assert.IsTrue(externalReferences.Shader.Filename.EndsWith(@"\Xml\foliage.fx"));
            });
        }

        [Test]
        public void FontDescription()
        {
            DeserializeCompileAndLoad<ExtendedFontDescription>("19_FontDescription.xml", fontDesc =>
            {
                Assert.AreEqual("Foo", fontDesc.FontName);
                Assert.AreEqual(30.0f, fontDesc.Size);
                Assert.AreEqual(0.75f, fontDesc.Spacing);
                Assert.AreEqual(true, fontDesc.UseKerning);
                Assert.AreEqual(FontDescriptionStyle.Bold, fontDesc.Style);
                Assert.AreEqual('*', fontDesc.DefaultCharacter);
                        
                var expectedCharacters = new List<char>();
                for (var c = HttpUtility.HtmlDecode("&#32;")[0]; c <= HttpUtility.HtmlDecode("&#126;")[0]; c++)
                    expectedCharacters.Add(c);

                expectedCharacters.Add(HttpUtility.HtmlDecode("&#916;")[0]);
                expectedCharacters.Add(HttpUtility.HtmlDecode("&#176;")[0]);

                var characters = new List<char>(fontDesc.Characters);
                foreach (var c in expectedCharacters)
                {
                    Assert.Contains(c, characters);
                    characters.Remove(c);
                }

                Assert.IsEmpty(characters);

                var expectedStrings = new List<string>()
                    {
                        "item0",
                        "item1",
                        "item2",
                    };
                var strings = new List<string>(fontDesc.ExtendedListProperty);
                foreach (var s in expectedStrings)
                {
                    Assert.Contains(s, strings);
                    strings.Remove(s);
                }

                Assert.IsEmpty(strings);
            });
        }

        [Test]
        public void SystemTypes()
        {
            DeserializeCompileAndLoad<SystemTypes>("20_SystemTypes.xml", sysTypes =>
            {
                Assert.AreEqual(TimeSpan.FromSeconds(42.5f), sysTypes.TimeSpan);
            });
        }
    }
}

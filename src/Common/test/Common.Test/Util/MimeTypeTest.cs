// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Text;
using Steeltoe.Common.Util;
using Xunit;

namespace Steeltoe.Common.Test.Util;

public sealed class MimeTypeTest
{
    [Fact]
    public void SlashInSubtype()
    {
        Assert.Throws<ArgumentException>(() => new MimeType("text", "/"));
    }

    [Fact]
    public void ToMimeTypeNoSubtype()
    {
        Assert.Throws<ArgumentException>(() => MimeType.ToMimeType("audio"));
    }

    [Fact]
    public void ToMimeTypeNoSubtypeSlash()
    {
        Assert.Throws<ArgumentException>(() => MimeType.ToMimeType("audio/"));
    }

    [Fact]
    public void ToMimeTypeIllegalType()
    {
        Assert.Throws<ArgumentException>(() => MimeType.ToMimeType("audio(/basic"));
    }

    [Fact]
    public void ToMimeTypeIllegalSubtype()
    {
        Assert.Throws<ArgumentException>(() => MimeType.ToMimeType("audio/basic)"));
    }

    [Fact]
    public void ToMimeTypeIllegalCharset()
    {
        Assert.Throws<ArgumentException>(() => MimeType.ToMimeType("text/html; charset=foo-bar"));
    }

    [Fact]
    public void ParseCharset()
    {
        const string s = "text/html; charset=iso-8859-1";
        var mimeType = MimeType.ToMimeType(s);
        Assert.Equal("text", mimeType.Type);
        Assert.Equal("html", mimeType.Subtype);
        Assert.Equal(Encoding.GetEncoding("ISO-8859-1"), mimeType.Encoding);
    }

    [Fact]
    public void ParseQuotedCharset()
    {
        const string s = "application/xml;charset=\"utf-8\"";
        var mimeType = MimeType.ToMimeType(s);
        Assert.Equal("application", mimeType.Type);
        Assert.Equal("xml", mimeType.Subtype);
        Assert.Equal(Encoding.UTF8.EncodingName, mimeType.Encoding.EncodingName);
    }

    [Fact]
    public void ParseQuotedSeparator()
    {
        const string s = "application/xop+xml;charset=utf-8;type=\"application/soap+xml;action=\\\"https://x.y.z\\\"\"";
        var mimeType = MimeType.ToMimeType(s);
        Assert.Equal("application", mimeType.Type);
        Assert.Equal("xop+xml", mimeType.Subtype);
        Assert.Equal(Encoding.UTF8.EncodingName, mimeType.Encoding.EncodingName);
        Assert.Equal("\"application/soap+xml;action=\\\"https://x.y.z\\\"\"", mimeType.GetParameter("type"));
    }

    [Fact]
    public void Includes()
    {
        MimeType textPlain = MimeTypeUtils.TextPlain;
        Assert.True(textPlain.Includes(textPlain));
        var allText = new MimeType("text");

        Assert.True(allText.Includes(textPlain));
        Assert.False(textPlain.Includes(allText));

        Assert.True(MimeTypeUtils.All.Includes(textPlain));
        Assert.False(textPlain.Includes(MimeTypeUtils.All));

        Assert.True(MimeTypeUtils.All.Includes(textPlain));
        Assert.False(textPlain.Includes(MimeTypeUtils.All));

        var applicationSoapXml = new MimeType("application", "soap+xml");
        var applicationWildcardXml = new MimeType("application", "*+xml");
        var suffixXml = new MimeType("application", "x.y+z+xml");

        Assert.True(applicationSoapXml.Includes(applicationSoapXml));
        Assert.True(applicationWildcardXml.Includes(applicationWildcardXml));
        Assert.True(applicationWildcardXml.Includes(suffixXml));

        Assert.True(applicationWildcardXml.Includes(applicationSoapXml));
        Assert.False(applicationSoapXml.Includes(applicationWildcardXml));
        Assert.False(suffixXml.Includes(applicationWildcardXml));

        Assert.False(applicationWildcardXml.Includes(MimeTypeUtils.ApplicationJson));
    }

    [Fact]
    public void IsCompatible()
    {
        MimeType textPlain = MimeTypeUtils.TextPlain;
        Assert.True(textPlain.IsCompatibleWith(textPlain));
        var allText = new MimeType("text");

        Assert.True(allText.IsCompatibleWith(textPlain));
        Assert.True(textPlain.IsCompatibleWith(allText));

        Assert.True(MimeTypeUtils.All.IsCompatibleWith(textPlain));
        Assert.True(textPlain.IsCompatibleWith(MimeTypeUtils.All));

        Assert.True(MimeTypeUtils.All.IsCompatibleWith(textPlain));
        Assert.True(textPlain.IsCompatibleWith(MimeTypeUtils.All));

        var applicationSoapXml = new MimeType("application", "soap+xml");
        var applicationWildcardXml = new MimeType("application", "*+xml");
        var suffixXml = new MimeType("application", "x.y+z+xml"); // SPR-15795

        Assert.True(applicationSoapXml.IsCompatibleWith(applicationSoapXml));
        Assert.True(applicationWildcardXml.IsCompatibleWith(applicationWildcardXml));
        Assert.True(applicationWildcardXml.IsCompatibleWith(suffixXml));

        Assert.True(applicationWildcardXml.IsCompatibleWith(applicationSoapXml));
        Assert.True(applicationSoapXml.IsCompatibleWith(applicationWildcardXml));
        Assert.True(suffixXml.IsCompatibleWith(applicationWildcardXml));

        Assert.False(applicationWildcardXml.IsCompatibleWith(MimeTypeUtils.ApplicationJson));
    }

    [Fact]
    public void TestToString()
    {
        var mimeType = new MimeType("text", "plain");
        string result = mimeType.ToString();
        Assert.Equal("text/plain", result);
    }

    [Fact]
    public void ParseMimeType()
    {
        const string s = "audio/*";
        MimeType mimeType = MimeTypeUtils.ParseMimeType(s);
        Assert.Equal("audio", mimeType.Type);
        Assert.Equal("*", mimeType.Subtype);
    }

    [Fact]
    public void ParseMimeTypeNoSubtype()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio"));
    }

    [Fact]
    public void ParseMimeTypeNoSubtypeSlash()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio/"));
    }

    [Fact]
    public void ParseMimeTypeTypeRange()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("*/json"));
    }

    [Fact]
    public void ParseMimeTypeIllegalType()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio(/basic"));
    }

    [Fact]
    public void ParseMimeTypeIllegalSubtype()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio/basic)"));
    }

    [Fact]
    public void ParseMimeTypeMissingTypeAndSubtype()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("     ;a=b"));
    }

    [Fact]
    public void ParseMimeTypeEmptyParameterAttribute()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio/*;=value"));
    }

    [Fact]
    public void ParseMimeTypeEmptyParameterValue()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio/*;attr="));
    }

    [Fact]
    public void ParseMimeTypeIllegalParameterAttribute()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio/*;attr<=value"));
    }

    [Fact]
    public void ParseMimeTypeIllegalParameterValue()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio/*;attr=v>alue"));
    }

    [Fact]
    public void ParseMimeTypeIllegalCharset()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("text/html; charset=foo-bar"));
    }

    [Fact]
    public void ParseMimeTypeQuotedParameterValue()
    {
        MimeType mimeType = MimeTypeUtils.ParseMimeType("audio/*;attr=\"v>alue\"");
        Assert.Equal("\"v>alue\"", mimeType.GetParameter("attr"));
    }

    [Fact]
    public void ParseMimeTypeSingleQuotedParameterValue()
    {
        MimeType mimeType = MimeTypeUtils.ParseMimeType("audio/*;attr='v>alue'");
        Assert.Equal("'v>alue'", mimeType.GetParameter("attr"));
    }

    [Fact]
    public void ParseMimeTypeWithSpacesAroundEquals()
    {
        MimeType mimeType = MimeTypeUtils.ParseMimeType("multipart/x-mixed-replace;boundary = --myboundary");
        Assert.Equal("--myboundary", mimeType.GetParameter("boundary"));
    }

    [Fact]
    public void ParseMimeTypeWithSpacesAroundEqualsAndQuotedValue()
    {
        MimeType mimeType = MimeTypeUtils.ParseMimeType("text/plain; foo = \" bar \" ");
        Assert.Equal("\" bar \"", mimeType.GetParameter("foo"));
    }

    [Fact]
    public void ParseMimeTypeIllegalQuotedParameterValue()
    {
        Assert.Throws<ArgumentException>(() => MimeTypeUtils.ParseMimeType("audio/*;attr=\""));
    }

    [Fact]
    public void ParseMimeTypes()
    {
        const string s = "text/plain, text/html, text/x-dvi, text/x-c";
        List<MimeType> mimeTypes = MimeTypeUtils.ParseMimeTypes(s);
        Assert.NotNull(mimeTypes);
        Assert.Equal(4, mimeTypes.Count);

        mimeTypes = MimeTypeUtils.ParseMimeTypes(null);
        Assert.NotNull(mimeTypes);
        Assert.Empty(mimeTypes);
    }

    [Fact]
    public void ParseMimeTypesWithQuotedParameters()
    {
        TestWithQuotedParameters("foo/bar;param=\",\"");
        TestWithQuotedParameters("foo/bar;param=\"s,a,\"");
        TestWithQuotedParameters("foo/bar;param=\"s,\"", "text/x-c");
        TestWithQuotedParameters("foo/bar;param=\"a\\\"b,c\"");
        TestWithQuotedParameters("foo/bar;param=\"\\\\\"");
        TestWithQuotedParameters("foo/bar;param=\"\\,\\\"");
    }

    [Fact]
    public void CompareTo()
    {
        var audioBasic = new MimeType("audio", "basic");
        var audio = new MimeType("audio");
        var audioWave = new MimeType("audio", "wave");
        var audioBasicLevel = new MimeType("audio", "basic", SingletonMap("level", "1"));

        // equal
        Assert.Equal(0, audioBasic.CompareTo(audioBasic));
        Assert.Equal(0, audio.CompareTo(audio));
        Assert.Equal(0, audioBasicLevel.CompareTo(audioBasicLevel));

        Assert.True(audioBasicLevel.CompareTo(audio) > 0);

        var expected = new List<MimeType>
        {
            audio,
            audioBasic,
            audioBasicLevel,
            audioWave
        };

        var result = new List<MimeType>(expected);

        // shuffle & sort 10 times
        for (int i = 0; i < 10; i++)
        {
            Shuffle(result);
            result.Sort();

            for (int j = 0; j < result.Count; j++)
            {
                Assert.Same(expected[j], result[j]);
            }
        }
    }

    [Fact]
    public void CompareToCaseSensitivity()
    {
        var m1 = new MimeType("audio", "basic");
        var m2 = new MimeType("Audio", "Basic");
        Assert.Equal(0, m1.CompareTo(m2));
        Assert.Equal(0, m2.CompareTo(m1));

        m1 = new MimeType("audio", "basic", SingletonMap("foo", "bar"));
        m2 = new MimeType("audio", "basic", SingletonMap("Foo", "bar"));
        Assert.Equal(0, m1.CompareTo(m2));
        Assert.Equal(0, m2.CompareTo(m1));

        m1 = new MimeType("audio", "basic", SingletonMap("foo", "bar"));
        m2 = new MimeType("audio", "basic", SingletonMap("foo", "Bar"));
        Assert.True(m1.CompareTo(m2) != 0);
        Assert.True(m2.CompareTo(m1) != 0);
    }

    [Fact]
    public void EqualsIsCaseInsensitiveForCharsets()
    {
        var m1 = new MimeType("text", "plain", SingletonMap("charset", "UTF-8"));
        var m2 = new MimeType("text", "plain", SingletonMap("charset", "utf-8"));
        Assert.Equal(m1, m2);
        Assert.Equal(m2, m1);
        Assert.Equal(0, m1.CompareTo(m2));
        Assert.Equal(0, m2.CompareTo(m1));
    }

    private void TestWithQuotedParameters(params string[] mimeTypes)
    {
        string s = string.Join(",", mimeTypes);
        List<MimeType> actual = MimeTypeUtils.ParseMimeTypes(s);
        Assert.Equal(mimeTypes.Length, actual.Count);

        for (int i = 0; i < mimeTypes.Length; i++)
        {
            Assert.Equal(mimeTypes[i], actual[i].ToString());
        }
    }

    private IDictionary<string, string> SingletonMap(string key, string value)
    {
        return new Dictionary<string, string>
        {
            { key, value }
        };
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            Swap(list, i, Random.Shared.Next(i, list.Count));
        }
    }

    private void Swap<T>(IList<T> list, int i, int j)
    {
        (list[i], list[j]) = (list[j], list[i]);
    }
}

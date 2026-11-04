using System.Text;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Filters.Bilingual.Xliff1;
using Blackbird.Filters.Bilingual.Xliff2;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;

namespace Apps.Payload.Utils;

public static class TranslatedFileReader
{
    public static string ReadHtml(byte[] bytes, string fileName)
    {
        var isXlf = Xliff2Serializer.IsXliff2(new MemoryStream(bytes), out _)
                    || Xliff1Serializer.IsXliff1(new MemoryStream(bytes), out _);

        if (!isXlf)
            return Encoding.UTF8.GetString(bytes);

        var loadResult = Transformation.Load(new MemoryStream(bytes), fileName);
        if (!loadResult.Success)
            throw new PluginApplicationException(loadResult.Error);

        var targetResult = loadResult.Value.Target();
        if (!targetResult.Success)
            throw new PluginApplicationException(targetResult.Error);

        return targetResult.Value.ToStream().ReadString();
    }
}

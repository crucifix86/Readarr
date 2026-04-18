using System;
using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;

namespace Readarr.Http.Frontend.Mappers
{
    public class ReaderHtmlMapper : HtmlMapperBase
    {
        public ReaderHtmlMapper(IAppFolderInfo appFolderInfo,
                                IDiskProvider diskProvider,
                                Lazy<ICacheBreakerProvider> cacheBreakProviderFactory,
                                IConfigFileProvider configFileProvider,
                                Logger logger)
            : base(diskProvider, cacheBreakProviderFactory, logger)
        {
            HtmlPath = Path.Combine(appFolderInfo.StartUpFolder, configFileProvider.UiFolder, "reader.html");
            UrlBase = configFileProvider.UrlBase;
        }

        public override string Map(string resourceUrl)
        {
            return HtmlPath;
        }

        public override bool CanHandle(string resourceUrl)
        {
            resourceUrl = resourceUrl.ToLowerInvariant();

            // /reader/api/* is the JSON controller — not our concern.
            if (resourceUrl.StartsWith("/reader/api"))
            {
                return false;
            }

            // Don't compete with StaticResourceMapper for JS/CSS bundle files.
            if (resourceUrl.Contains("."))
            {
                return false;
            }

            return resourceUrl == "/reader" || resourceUrl.StartsWith("/reader/");
        }
    }
}

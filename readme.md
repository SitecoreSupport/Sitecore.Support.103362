# Sitecore.Support.103362
Fallback item versions are dumped incorrectly on serialization or package building. As a result, further package installation or reverting of such an item creates real versions for all the fallback languages. This patch fixes the BuildPackage control and serialization commands (itemsync:dumpitem and itemsync:dumptree).

The bug is fixed in Sitecore 8.2 Update-1.

## License  
This patch is licensed under the [Sitecore Corporation A/S License for GitHub](https://github.com/sitecoresupport/Sitecore.Support.103362/blob/master/LICENSE).  

## Download  
Downloads are available via [GitHub Releases](https://github.com/sitecoresupport/Sitecore.Support.103362/releases).  

[![Github All Releases](https://img.shields.io/github/downloads/SitecoreSupport/Sitecore.Support.103362/total.svg)](https://github.com/SitecoreSupport/Sitecore.Support.103362/releases)

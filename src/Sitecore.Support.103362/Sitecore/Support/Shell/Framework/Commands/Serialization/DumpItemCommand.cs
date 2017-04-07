namespace Sitecore.Support.Shell.Framework.Commands.Serialization
{
    using Sitecore.Data.Items;
    using Sitecore.Data.LanguageFallback;
    using Sitecore.Shell.Framework.Commands.Serialization;
    using System;

    public class DumpItemCommand : Sitecore.Shell.Framework.Commands.Serialization.DumpItemCommand
    {
        protected override void Dump(Item item)
        {
            using (new LanguageFallbackItemSwitcher(false))
            {
                base.Dump(item);
            }
        }
    }
}


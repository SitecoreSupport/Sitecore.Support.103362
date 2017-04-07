namespace Sitecore.Support.Shell.Framework.Commands.Serialization
{
    using Sitecore.Data.Items;
    using Sitecore.Data.LanguageFallback;
    using Sitecore.Shell.Framework.Commands.Serialization;
    using System;

    public class DumpTreeCommand : Sitecore.Shell.Framework.Commands.Serialization.DumpTreeCommand
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


using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Web;
using Sitecore.Web.UI.WebControls;
using System;
using System.Web.UI;

namespace Sitecore.Support.Shell.Controls.TreeviewEx
{
    public class TreeviewExPage : Page
    {
        /// <summary>
        /// Handles the Load event of the Page control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="T:System.EventArgs" /> instance containing the event data.</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(e, "e");
            Web.UI.WebControls.TreeviewEx treeviewEx = MainUtil.GetBool(WebUtil.GetQueryString("mr"), false) ? new MultiRootTreeview() : new Web.UI.WebControls.TreeviewEx();
            Controls.Add(treeviewEx);
            treeviewEx.ID = WebUtil.GetQueryString("treeid");
            string queryString = WebUtil.GetQueryString("db", Client.ContentDatabase.Name);
            Database database = Factory.GetDatabase(queryString);
            Assert.IsNotNull(database, queryString);
            ID itemId = ShortID.DecodeID(WebUtil.GetQueryString("id"));
            string queryString2 = WebUtil.GetQueryString("la");
            Language result;
            if (string.IsNullOrEmpty(queryString2) || !Language.TryParse(queryString2, out result))
            {
                // Fix 397630
                string siteLanguage = Context.Request.Cookies[$"{Context.Request.Cookies["sxa_site"]?.Value}#lang"]?.Value;
                result = (string.IsNullOrEmpty(siteLanguage) ? Sitecore.Context.Language : Language.Parse(siteLanguage));
                // end of fix
            }
            Item item = database.GetItem(itemId, result);
            if (item != null)
            {
                treeviewEx.ParentItem = item;
            }
        }
    }
}
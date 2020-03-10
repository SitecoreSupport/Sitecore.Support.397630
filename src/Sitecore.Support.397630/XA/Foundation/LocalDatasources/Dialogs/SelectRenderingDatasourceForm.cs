using Microsoft.Extensions.DependencyInjection;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Templates;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Shell.Applications.Dialogs;
using Sitecore.Shell.Applications.Dialogs.ItemLister;
using Sitecore.Shell.Applications.Dialogs.SelectCreateItem;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;
using Sitecore.XA.Foundation.Abstractions;
using Sitecore.XA.Foundation.LocalDatasources.RenderingDatasources;
using Sitecore.XA.Foundation.LocalDatasources.Services;
using Sitecore.XA.Foundation.Multisite;
using Sitecore.XA.Foundation.Presentation;
using Sitecore.XA.Foundation.SitecoreExtensions.Controls;
using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
using Sitecore.XA.Foundation.SitecoreExtensions.Repositories;
using Sitecore.XA.Foundation.SitecoreExtensions.Services;
using Sitecore.XA.Foundation.SitecoreExtensions.Utils;
using Sitecore.XA.Foundation.SitecoreExtensions.VersionDecoupling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace Sitecore.Support.XA.Foundation.LocalDatasources.Dialogs
{
    public class SelectRenderingDatasourceForm : SelectCreateItemForm
    {
        private static Func<ClientPage, ArrayList> _commandsFieldGetter;
        private string _contextItemPath;
        private string _renderingItemId;
        private Item _contextItem;
        private Item _currentRendering;
        private Item _currentRenderingDatasourceTemplate;
        private SelectItemOptions _selectOptions;
        public IDatasourceSettingsProvider DatasourceSettingsProvider;
        protected IContentRepository ContentRepository;
        protected ILocalDatasourceService LocalDatasourceService;
        protected Literal Information;
        protected Edit ItemLink;
        protected Literal PathResolve;
        protected Border SearchOption;
        protected Border SearchSection;
        protected Literal SectionHeader;
        protected Border SelectOption;
        protected Scrollbox SelectSection;
        protected Border Warnings;

        protected Item CurrentRendering
        {
            get
            {
                return this._currentRendering ?? (this._currentRendering = this.ContentRepository.GetItem(ID.Parse(this.RenderingItemId)));
            }
        }

        protected Item CurrentRenderingDatasourceTemplate
        {
            get
            {
                return this._currentRenderingDatasourceTemplate ?? (this._currentRenderingDatasourceTemplate = this.ContentRepository.GetItem(this.CurrentRendering[Sitecore.XA.Foundation.LocalDatasources.Templates.RenderingOptions.Fields.DatasourceTemplate]));
            }
        }

        protected Language ContentLanguage
        {
            get
            {
                return this.ServerProperties["cont_language"] as Language;
            }
            set
            {
                this.ServerProperties["cont_language"] = (object)value;
            }
        }

        protected virtual Item CurrentDatasourceItem
        {
            get
            {
                string path = this.LocalDatasourceService.ExpandPageRelativePath(this.CurrentDatasourcePath, this.ContextItemPath);
                if (string.IsNullOrEmpty(path))
                    return (Item)null;
                return this.ContentRepository.GetItem(path);
            }
        }

        protected string CurrentDatasourcePath
        {
            get
            {
                return this.ServerProperties["current_datasource"] as string;
            }
            set
            {
                this.ServerProperties["current_datasource"] = (object)value;
            }
        }

        protected string ContextItemPath
        {
            get
            {
                return this._contextItemPath ?? (this._contextItemPath = WebUtil.GetQueryString("cip"));
            }
        }

        protected string RenderingItemId
        {
            get
            {
                return this._renderingItemId ?? (this._renderingItemId = WebUtil.GetQueryString("r"));
            }
        }

        protected Item ContextItem
        {
            get
            {
                return this._contextItem ?? (this._contextItem = this.ContentRepository.GetItem(this.ContextItemPath));
            }
        }

        protected Item Prototype
        {
            get
            {
                ItemUri prototypeUri = this.PrototypeUri;
                if (!(prototypeUri != (ItemUri)null))
                    return (Item)null;
                return Database.GetItem(prototypeUri);
            }
            set
            {
                Assert.IsNotNull((object)value, nameof(value));
                this.ServerProperties["template_item"] = (object)value.Uri;
            }
        }

        protected ItemUri PrototypeUri
        {
            get
            {
                return this.ServerProperties["template_item"] as ItemUri;
            }
        }

        protected override Sitecore.Web.UI.HtmlControls.Control SelectOptionControl
        {
            get
            {
                return (Sitecore.Web.UI.HtmlControls.Control)this.SelectOption;
            }
        }

        protected IContext Context { get; }

        protected override Sitecore.Web.UI.HtmlControls.Control CreateOptionControl { get; }

        public SelectRenderingDatasourceForm()
        {
            this.ContentRepository = ServiceLocator.ServiceProvider.GetService<IContentRepository>();
            this.LocalDatasourceService = ServiceLocator.ServiceProvider.GetService<ILocalDatasourceService>();
            this.DatasourceSettingsProvider = ServiceLocator.ServiceProvider.GetService<IDatasourceSettingsProvider>();
            this.Context = ServiceLocator.ServiceProvider.GetService<IContext>();
        }

        protected void CopyDataSource(string sourceRootId)
        {
            ID itemId = ID.Parse(sourceRootId);
            if (this.ContextItem.Database.GetItem(itemId) == null)
                return;
            this.Context.ClientPage.Start((object)this, "CopyDatasourceClientPipeline", new NameValueCollection()
            {
                ["itemid"] = itemId.ToString(),
                ["language"] = this.ContextItem.Language.Name,
                ["test"] = "testvalue"
            });
        }

        protected void CreateDataSource(string id)
        {
            if (this.ContextItem.Database.GetItem(id) == null)
                return;
            this.Context.ClientPage.Start((object)this, "CreateDatasourceClientPipeline", new NameValueCollection()
            {
                ["itemid"] = id,
                ["language"] = this.ContextItem.Language.Name,
                ["test"] = "testvalue"
            });
        }

        protected virtual void CopyDatasourceClientPipeline(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull((object)args, nameof(args));
            Item itemNotNull = Client.GetItemNotNull(args.Parameters["itemid"], Language.Parse(args.Parameters["language"]));
            if (args.IsPostBack)
            {
                if (!args.HasResult)
                    return;
                NameValueCollection parameters = WebUtil.ParseParameters(args.Result, ',');
                Item targetLocation = this.GetTargetLocation(ID.Parse(parameters["itemId"]), itemNotNull);
                if (targetLocation == null)
                    return;
                Item copiedItem = itemNotNull.CopyTo(targetLocation, parameters["name"]);
                if (copiedItem == null)
                    return;
                this.UpdateTreeDataContexts(targetLocation, copiedItem);
            }
            else
            {
                SelectItemBasicOptions itemBasicOptions = SelectItemOptions.Parse<SelectItemBasicOptions>();
                itemBasicOptions.DatasourceRoots = (IEnumerable<Item>)itemBasicOptions.DatasourceRoots.Where<Item>((Func<Item, bool>)(root => !root.ID.ToString().Equals(Sitecore.XA.Foundation.LocalDatasources.Items.VirtualPageStandardValue.ToString()))).ToList<Item>();
                itemBasicOptions.Title = Translate.Text("Select root to copy datasource");
                itemBasicOptions.Text = Translate.Text("Select the root item and the name for the copy.");
                UrlString urlString = itemBasicOptions.ToUrlString();
                urlString.Parameters.Add("defaultName", itemNotNull.Name);
                SheerResponse.ShowModalDialog(urlString.ToString(), true);
                args.WaitForPostBack();
            }
        }

        protected virtual Item GetTargetLocation(ID targetId, Item item)
        {
            Item obj;
            if (targetId == Sitecore.XA.Foundation.LocalDatasources.Items.VirtualPageDataStandardValue)
            {
                IEnumerable<Item> list = (IEnumerable<Item>)this.ContextItem.ChildrenInheritingFrom(Sitecore.XA.Foundation.Editing.Templates.PageDataFolder).ToList<Item>();
                obj = list.Any<Item>() ? list.First<Item>() : this.CreatePageDataFolder();
            }
            else
                obj = item.Database.GetItem(targetId);
            return obj;
        }

        protected virtual void UpdateTreeDataContexts(Item targetLocation, Item copiedItem)
        {
            string str = this.RemoveStandardValuesDataContext();
            if (string.IsNullOrWhiteSpace(str))
                return;
            ListString listString = new ListString(((IEnumerable<string>)this.Treeview.DataContext.Split('|')).ToList<string>());
            listString.Remove(str);
            DataContext contextForPageData = this.CreateDataContextForPageData(targetLocation);
            listString.Add(contextForPageData.ID);
            this.Treeview.SelectedIDs.Clear();
            ((MultiRootTreeview)this.Treeview).CurrentDataContext = contextForPageData.ID;
            this.Treeview.DataContext = listString.ToString();
            this.Treeview.RefreshRoot();
        }

        protected virtual string RemoveStandardValuesDataContext()
        {
            string str = string.Empty;
            foreach (DataContext dataContext in WebUtil.FindControlsOfType(typeof(DataContext), (System.Web.UI.Control)this.Context.ClientPage))
            {
                Item folder = dataContext.GetFolder();
                if (folder != null && folder.ID == Sitecore.XA.Foundation.LocalDatasources.Items.VirtualPageDataStandardValue)
                {
                    dataContext.Parent.Controls.Remove((System.Web.UI.Control)dataContext);
                    str = dataContext.ID;
                }
            }
            return str;
        }

        protected virtual DataContext CreateDataContextForPageData(Item rootItem)
        {
            DataContext dataContext = this.CopyDataContext(this.DataContext, rootItem.ID.ToString());
            dataContext.Root = rootItem.Paths.FullPath;
            dataContext.Folder = this.CurrentDatasourceItem.ID.ToString();
            this.Context.ClientPage.AddControl((System.Web.UI.Control)this.Dialog, (System.Web.UI.Control)dataContext);
            return dataContext;
        }

        protected void CreateLocalDataSource(string id)
        {
            this.Context.ClientPage.Start((object)this, "CreateLocalDatasourceClientPipeline", new NameValueCollection()
            {
                ["itemid"] = id,
                ["language"] = this.ContextItem.Language.Name,
                ["test"] = "testvalue"
            });
        }

        protected virtual void CreateLocalDatasourceClientPipeline(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull((object)args, nameof(args));
            Item item = Client.GetItemNotNull(args.Parameters["itemid"], Language.Parse(args.Parameters["language"]));
            if (args.HasResult && !args.IsPostBack)
                args.AbortPipeline();
            else if (args.IsPostBack)
            {
                if (!args.HasResult)
                    return;
                string itemName = args.Result;
                if (item == null)
                {
                    SheerResponse.Alert("Select an item first.");
                }
                else
                {
                    string validationErrorMessage;
                    if (!this.ValidateNewItemName(itemName, out validationErrorMessage))
                        SheerResponse.Alert(validationErrorMessage);
                    else if (item.Children.Any<Item>((Func<Item, bool>)(child => item.Name.Equals(itemName, StringComparison.InvariantCultureIgnoreCase))))
                    {
                        SheerResponse.Alert(Translate.Text("An item with that name already exists."));
                    }
                    else
                    {
                        Language contentLanguage = this.ContentLanguage;
                        if (contentLanguage != (Language)null && contentLanguage != item.Language)
                            item = item.Database.GetItem(item.ID, contentLanguage) ?? item;
                        TemplateItem templateItem = (TemplateItem)this.ContentRepository.GetItem(Sitecore.XA.Foundation.LocalDatasources.Items.VirtualPageData);
                        if (templateItem != null && templateItem.StandardValues != null && item.ID == templateItem.StandardValues.ID)
                            item = this.CreatePageDataFolder();
                        this.DisableContextsEvents();
                        if (this.Prototype == null)
                            this.FillDatasourceTemplate(item);
                        if (this.Prototype != null)
                        {
                            Item selectedItem = this.Prototype == null || !(this.Prototype.TemplateID == TemplateIDs.BranchTemplate) ? item.Add(itemName, (TemplateItem)this.Prototype) : item.Add(itemName, (BranchItem)this.Prototype);
                            this.EnableContextsEvents();
                            if (selectedItem != null)
                                this.SetDialogResult(selectedItem);
                            SheerResponse.CloseWindow();
                        }
                        else
                            SheerResponse.Alert(Translate.Text("Rendering datasource cannot be created. Rendering datasource template is not sets."));
                    }
                }
            }
            else if (!item.Access.CanCreate())
            {
                SheerResponse.Alert("You do not have permission to create an item here.");
            }
            else
            {
                SheerResponse.Input(Translate.Text("Please provide datasource name"), string.Empty);
                args.WaitForPostBack();
            }
        }

        protected virtual void CreateDatasourceClientPipeline(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull((object)args, nameof(args));
            Item item = Client.GetItemNotNull(args.Parameters["itemid"], Language.Parse(args.Parameters["language"]));
            if (args.HasResult && !args.IsPostBack)
                args.AbortPipeline();
            else if (args.IsPostBack)
            {
                if (!args.HasResult)
                    return;
                string[] strArray = args.Result.Split(',');
                string index = ShortID.IsShortID(strArray[0]) ? ShortID.DecodeID(strArray[0]).ToString() : strArray[0];
                string name = HttpUtility.UrlDecode(strArray[1]);
                BranchItem branch = Client.ContentDatabase.Branches[index, item.Language];
                Assert.IsNotNull((object)branch, typeof(BranchItem));
                this.DisableContextsEvents();
                Item selectedItem = this.Context.Workflow.AddItem(name, branch, item);
                this.DisableContextsEvents();
                if (selectedItem == null || !this.IsSelectable(selectedItem))
                    return;
                this.SetDialogResult(selectedItem);
                SheerResponse.CloseWindow();
            }
            else if (!item.Access.CanCreate())
                SheerResponse.Alert("You do not have permission to create an item here.");
            else
                SitecoreVersion.V82.IsAtMost((System.Action)(() =>
                {
                    UrlString urlString = new UrlString("/sitecore/client/Applications/ExperienceEditor/Dialogs/InsertPage");
                    item.Uri.AddToUrlString(urlString);
                    SheerResponse.ShowModalDialog(urlString.ToString(), true);
                    args.WaitForPostBack();
                })).Else((System.Action)(() =>
                {
                    UrlString urlString = new UrlString(string.Format("{0}?itemId={1}", (object)"/sitecore/client/Applications/ExperienceEditor/Dialogs/InsertPage", (object)WebUtil.UrlEncode(item.ID.ToString())));
                    item.Uri.AddToUrlString(urlString);
                    SheerResponse.ShowModalDialog(urlString.ToString(), "1200px", "700px", string.Empty, true);
                    args.WaitForPostBack();
                }));
        }

        protected override void ChangeMode(string mode)
        {
            Assert.ArgumentNotNull((object)mode, nameof(mode));
            base.ChangeMode(mode);
            if (UIUtil.IsIE())
                return;
            SheerResponse.Eval("scForm.browser.initializeFixsizeElements();");
        }

        protected UrlString GetUrl(Item item)
        {
            Assert.ArgumentNotNull((object)item, nameof(item));
            UrlString urlString = new UrlString();
            urlString.Append("id", item.ID.ToString());
            urlString.Append("la", item.Language.ToString());
            urlString.Append("vs", item.Version.ToString());
            urlString.Append("db", item.Database.Name);
            return urlString;
        }

        protected virtual DataContext CopyDataContext(DataContext dataContext, string id)
        {
            Assert.ArgumentNotNull((object)dataContext, nameof(dataContext));
            Assert.ArgumentNotNull((object)id, nameof(id));
            DataContext dataContext1 = new DataContext();
            dataContext1.Filter = dataContext.Filter;
            dataContext1.DataViewName = dataContext.DataViewName;
            dataContext1.ID = id;
            return dataContext1;
        }

        protected virtual void DisableContextsEvents()
        {
            this.DataContext.DisableEvents();
        }

        protected virtual void EnableContextsEvents()
        {
            this.DataContext.EnableEvents();
        }

        protected virtual Item CreatePageDataFolder()
        {
            if (this.ContextItem == null || !this.ContextItem.Access.CanCreate())
                return (Item)null;
            Item contextItem = this.ContextItem;
            NameValueCollection parameters = ServiceLocator.ServiceProvider.GetService<ISuspendedPipelineService>().GetSuspendedPipelineArgs((Func<ClientPipelineArgs, bool>)(args => ((IEnumerable<string>)args.Parameters.AllKeys).Contains<string>("SXA::LocalDataFolderParent")))?.Parameters;
            if (parameters != null)
            {
                string itemUri = parameters["SXA::LocalDataFolderParent"];
                if (itemUri != null)
                    contextItem = ServiceLocator.ServiceProvider.GetService<IContentRepository>().GetItem(new ItemUri(itemUri));
            }
            this.DisableContextsEvents();
            Item obj = this.Context.Workflow.AddItem("Data", new TemplateID(Sitecore.XA.Foundation.Editing.Templates.PageDataFolder), contextItem);
            this.EnableContextsEvents();
            return obj;
        }

        protected virtual void FixLastRefreshCommand(Item oldRoot, Item newRoot)
        {
            ArrayList arrayList = InternalInvoker.GetField<ClientPage, ArrayList>(ref SelectRenderingDatasourceForm._commandsFieldGetter, "_commands")(this.Context.ClientPage);
            ClientCommand clientCommand = arrayList[arrayList.Count - 1] as ClientCommand;
            if (clientCommand == null)
                return;
            clientCommand.Attributes["id"] = clientCommand.Attributes["id"].Replace(oldRoot.ID.ToShortID().ToString(), newRoot.ID.ToShortID().ToString());
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull((object)e, nameof(e));
            base.OnLoad(e);
            if (this.Context.ClientPage.IsEvent)
                return;
            this.SelectOption.Click = string.Format("ChangeMode(\"{0}\")", (object)"Select");
            this.SearchOption.Click = string.Format("ChangeMode(\"{0}\")", (object)"Search");
            SelectDatasourceOptions datasourceOptions = SelectItemOptions.Parse<SelectDatasourceOptions>();
            this._selectOptions = SelectItemOptions.Parse<SelectItemOptions>();
            if (datasourceOptions.DatasourcePrototype != null)
                this.Prototype = datasourceOptions.DatasourcePrototype;
            if (datasourceOptions.ContentLanguage != (Language)null)
                this.ContentLanguage = datasourceOptions.ContentLanguage;
            if (!string.IsNullOrEmpty(datasourceOptions.CurrentDatasource))
            {
                this.CurrentDatasourcePath = this.LocalDatasourceService.ExpandPageRelativePath(datasourceOptions.CurrentDatasource, this.ContextItemPath);
                Item obj = this.ContentRepository.GetItem(this.CurrentDatasourcePath);
                if (obj != null)
                {
                    Literal pathResolve = this.PathResolve;
                    if (pathResolve != null)
                        pathResolve.Text = pathResolve.Text + " " + obj.Paths.FullPath;
                }
                if (datasourceOptions.DatasourceRoots.FirstOrDefault<Item>() != null)
                {
                    string empty = string.Empty;
                    if (!string.IsNullOrEmpty(datasourceOptions.DatasourceItemDefaultName))
                        ItemUtil.GetCopyOfName(datasourceOptions.DatasourceRoots.FirstOrDefault<Item>(), datasourceOptions.DatasourceItemDefaultName);
                }
            }
            this.ExcludeSystemTemplatesForDisplay(datasourceOptions, this.ContextItem);
            this.DataContext.Filter = this.GetFilter((SelectItemOptions)datasourceOptions);
            this.SetDataContexts(datasourceOptions.DatasourceRoots);
            this.SetControlsForSelection(this.DataContext.GetFolder());
            this.SetSectionHeader();
        }

        protected virtual void ExcludeSystemTemplatesForDisplay(
          SelectDatasourceOptions datasourceOptions,
          Item contextItem)
        {
            List<Template> templateList = new List<Template>();
            IMultisiteContext service1 = ServiceLocator.ServiceProvider.GetService<IMultisiteContext>();
            IPresentationContext service2 = ServiceLocator.ServiceProvider.GetService<IPresentationContext>();
            if (service1 == null || service2 == null)
                return;
            Item settingsItem = service1.GetSettingsItem(contextItem);
            Item presentationItem = service2.GetPresentationItem(contextItem);
            if (settingsItem != null)
            {
                Template template = TemplateManager.GetTemplate(settingsItem.TemplateID, contextItem.Database);
                templateList.Add(template);
            }
            if (presentationItem != null)
            {
                Template template = TemplateManager.GetTemplate(presentationItem.TemplateID, contextItem.Database);
                templateList.Add(template);
            }
            templateList.AddRange((IEnumerable<Template>)datasourceOptions.ExcludeTemplatesForDisplay);
            datasourceOptions.ExcludeTemplatesForDisplay = templateList;
        }

        protected override void OnOK(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, nameof(sender));
            Assert.ArgumentNotNull((object)args, nameof(args));
            string currentMode = this.CurrentMode;
            string str = currentMode;
            if (str == null)
                return;
            if (str != "Select")
            {
                if (currentMode != "Search")
                    return;
                Item selectedItem = this.ContentRepository.GetItem(this.ItemLink.Value);
                if (selectedItem != null)
                {
                    Literal pathResolve = this.PathResolve;
                    if (pathResolve != null)
                        pathResolve.Text = selectedItem.Paths.FullPath;
                    this.SetDialogResult(selectedItem);
                    SheerResponse.CloseWindow();
                }
                else
                    SheerResponse.Alert("Please select an item from the results");
            }
            else
            {
                Item selectionItem = this.Treeview.GetSelectionItem();
                if (selectionItem != null)
                {
                    Literal pathResolve = this.PathResolve;
                    if (pathResolve != null)
                        pathResolve.Text = selectionItem.Paths.FullPath;
                    this.SetDialogResult(selectionItem);
                }
                SheerResponse.CloseWindow();
            }
        }

        protected override void SetDialogResult(Item selectedItem)
        {
            Assert.ArgumentNotNull((object)selectedItem, nameof(selectedItem));
            if (selectedItem.ID.Equals(Sitecore.XA.Foundation.LocalDatasources.Items.VirtualPageStandardValue))
                SheerResponse.SetDialogValue("page:");
            else if (selectedItem.GetParentOfTemplate(Sitecore.XA.Foundation.Editing.Templates.PageDataFolder) == null)
            {
                base.SetDialogResult(selectedItem);
            }
            else
            {
                string fullPath = selectedItem.Paths.FullPath;
                if (!string.IsNullOrEmpty(this.ContextItemPath) && fullPath.StartsWith(this.ContextItemPath, StringComparison.OrdinalIgnoreCase))
                    SheerResponse.SetDialogValue("local:" + fullPath.Substring(this.ContextItemPath.Length));
                else
                    base.SetDialogResult(selectedItem);
            }
        }

        protected virtual void SetControlsForCloning(Item item)
        {
            this.SetControlsForCreating(item);
        }

        protected virtual void SetControlsForCreating(Item item)
        {
            this.Warnings.Visible = false;
            SheerResponse.SetAttribute(this.Warnings.ID, "title", string.Empty);
            string errorMessage;
            if (!this.CanCreateItem(item, out errorMessage) && item.ID != Sitecore.XA.Foundation.LocalDatasources.Items.VirtualPageDataStandardValue)
            {
                this.OK.Disabled = true;
                this.Information.Text = Translate.Text(errorMessage);
                this.Warnings.Visible = true;
            }
            else
                this.OK.Disabled = false;
        }

        protected virtual void SetControlsForSearching(Item item)
        {
            this.Warnings.Visible = false;
            SheerResponse.SetAttribute(this.Warnings.ID, "title", string.Empty);
            string errorMessage;
            if (!this.CanCreateItem(item, out errorMessage))
            {
                this.OK.Disabled = true;
                this.Information.Text = Translate.Text(errorMessage);
                this.Warnings.Visible = true;
            }
            else
                this.OK.Disabled = false;
        }

        protected virtual void SetControlsForSelection(Item item)
        {
            this.Warnings.Visible = false;
            SheerResponse.SetAttribute(this.Warnings.ID, "title", string.Empty);
            if (item == null)
                return;
            if (!this.IsSelectable(item))
            {
                this.OK.Disabled = true;
                this.Information.Text = Translate.Text(string.Format("The '{0}' item is not a valid selection.", (object)StringUtil.Clip(item.DisplayName, 20, true)));
                this.Warnings.Visible = true;
                SheerResponse.SetAttribute(this.Warnings.ID, "title", string.Format(Translate.Text("The data source must be a '{0}' item."), (object)this.TemplateNamesString));
            }
            else
            {
                this.Information.Text = string.Empty;
                this.OK.Disabled = false;
            }
        }

        protected override void SetControlsOnModeChange()
        {
            string currentMode = this.CurrentMode;
            if (currentMode != null)
            {
                this.SelectOption.Class = string.Empty;
                this.SearchOption.Class = string.Empty;
                this.SearchSection.Visible = false;
                this.SelectSection.Visible = false;
                if (currentMode != "Select")
                {
                    if (currentMode == "Search")
                    {
                        this.SearchOption.Class = "selected";
                        this.SearchSection.Visible = true;
                    }
                }
                else
                {
                    this.SelectOption.Class = "selected";
                    this.SelectSection.Visible = true;
                    this.SetControlsForSelection(this.Treeview.GetSelectionItem());
                }
            }
            this.SetSectionHeader();
        }

        protected virtual void SetDataContexts(IEnumerable<Item> roots)
        {
            Item currentDatasourceItem = this.CurrentDatasourceItem;
            int num = 0;
            ListString listString = new ListString();
            bool flag = false;
            foreach (Item descendantItem in (IEnumerable<Item>)(roots as IList<Item> ?? (IList<Item>)roots.ToList<Item>()))
            {
                DataContext dataContext = this.CopyDataContext(this.DataContext, "SelectDataContext" + (object)num);
                // Fix 397630
                if (Context.Request.QueryString["clang"] != null)
                {
                    dataContext.Language = Language.Parse(Context.Request.QueryString["clang"]);
                }
                // end of fix
                dataContext.Root = descendantItem.Paths.FullPath;
                if (currentDatasourceItem != null && !flag)
                    dataContext.DefaultItem = currentDatasourceItem.ID.ToString();
                if (currentDatasourceItem != null && (currentDatasourceItem.ID == descendantItem.ID || currentDatasourceItem.Paths.IsDescendantOf(descendantItem)) && !flag)
                {
                    dataContext.Folder = currentDatasourceItem.ID.ToString();
                    ((MultiRootTreeview)this.Treeview).CurrentDataContext = dataContext.ID;
                    flag = true;
                }
                this.Context.ClientPage.AddControl((System.Web.UI.Control)this.Dialog, (System.Web.UI.Control)dataContext);
                listString.Add(dataContext.ID);
                ++num;
            }
            this.Treeview.DataContext = listString.ToString();
        }

        protected virtual void SetSectionHeader()
        {
            string currentMode = this.CurrentMode;
            if (!(currentMode == "Select"))
            {
                if (!(currentMode == "Search"))
                    return;
                this.SectionHeader.Text = Translate.Text("Search for content items");
            }
            else
                this.SectionHeader.Text = Translate.Text("Select an existing content item.");
        }

        protected void Treeview_Click()
        {
            this.SetControlsForSelection(this.Treeview.GetSelectionItem());
        }

        protected virtual TemplateItem GetFolderTemplate(Item parentItem)
        {
            ID templateId = this.Prototype == null ? this.CurrentRenderingDatasourceTemplate?.ID : this.Prototype.ID;
            return Sitecore.Data.Masters.Masters.GetMasters(parentItem).Where<Item>((Func<Item, bool>)(t =>
            {
                if (t.TemplateID == TemplateIDs.Template)
                    return ((TemplateItem)t).DoesTemplateInheritFrom(TemplateIDs.Folder);
                return false;
            })).Select<Item, Item>((Func<Item, Item>)(t => ((TemplateItem)t).StandardValues)).Where<Item>((Func<Item, bool>)(s => s != null)).Where<Item>((Func<Item, bool>)(s => Sitecore.Data.Masters.Masters.GetMasters(s).Any<Item>((Func<Item, bool>)(c => c.ID == templateId)))).Select<Item, TemplateItem>((Func<Item, TemplateItem>)(s => s.Template)).FirstOrDefault<TemplateItem>();
        }

        protected virtual void FillDatasourceTemplate(Item parent)
        {
            IEnumerable<Item> compatibleRenderings = this.DatasourceSettingsProvider.GetCompatibleRenderings(this.ContextItem, ID.Parse(this.RenderingItemId));
            TemplateItem templateItem = (TemplateItem)this.ContentRepository.GetItem(parent.TemplateID);
            if (templateItem.StandardValues != null)
            {
                string[] strArray = templateItem.StandardValues[Sitecore.XA.Foundation.LocalDatasources.Templates.InsertOptions.Fields.__Masters].Split('|');
                foreach (BaseItem baseItem in compatibleRenderings)
                {
                    Item obj = this.ContentRepository.GetItem(baseItem[Sitecore.XA.Foundation.LocalDatasources.Templates.RenderingOptions.Fields.DatasourceTemplate]);
                    if (((IEnumerable<string>)strArray).Contains<string>(obj.ID.ToString()))
                    {
                        this.Prototype = obj;
                        break;
                    }
                }
            }
            if (this.Prototype != null || this.CurrentRenderingDatasourceTemplate == null)
                return;
            this.Prototype = this.CurrentRenderingDatasourceTemplate;
        }

        protected override bool IsSelectable(Item item)
        {
            if (Sitecore.XA.Foundation.LocalDatasources.Items.VirtualPage.Equals(item.TemplateID))
                return true;
            return base.IsSelectable(item);
        }
    }
}
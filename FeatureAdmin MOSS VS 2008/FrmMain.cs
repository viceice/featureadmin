using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;

namespace FeatureAdmin
{
    public partial class FrmMain : Form
    {

        #region members

        // warning, when no feature was selected
        public const string NOFEATURESELECTED = "No feature selected. Please select at least 1 feature.";

        // defines, how the format of the log time
        public static string DATETIMEFORMAT = "yyyy/MM/dd HH:mm:ss";
        // prefix for log entries: Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - "; 

        private FeatureManager farmFeatureDefinitionsManager = null;
        private FeatureManager siteFeatureManager = null;
        private FeatureManager webFeatureManager = null;
        private static SPWebApplication m_CurrentWebApp;
        private static SPSite m_CurrentSite;
        private static SPWeb m_CurrentWeb;
        // private SPList m_CurrentList;

        #endregion


        /// <summary>Initialize Main Window</summary>
        public FrmMain()
        {
            InitializeComponent();

            // web app list is prefilled
            loadWebAppList();

            removeBtnEnabled(false);

            featDefBtnEnabled(false); ;

        }

        #region FeatureDefinition Methods

        /// <summary>Used to populate the list of Farm Feature Definitions</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLoadFDefs_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            this.clbFeatureDefinitions.Items.Clear();

            farmFeatureDefinitionsManager = new FeatureManager("Farm FeatureDefinitions");
            farmFeatureDefinitionsManager.AddFeatures(SPFarm.Local.FeatureDefinitions);
            farmFeatureDefinitionsManager.Features.Sort();

            //clbFeatureDefinitions.
            this.clbFeatureDefinitions.Items.AddRange(farmFeatureDefinitionsManager.Features.ToArray());
            this.txtResult.Text += BuildFeatureLog(farmFeatureDefinitionsManager.Url, farmFeatureDefinitionsManager.Features);



            logDateMsg("Feature Definition list updated.");

            Cursor.Current = Cursors.Default;
            // tabControl1.Enabled = false;
            // tabControl1.Visible = false;
            // listFeatures.Items.Clear();
            // listDetails.Items.Clear();
        }


        /// <summary>Uninstall the selected Feature definition</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnUninstFeatureDef_Click(object sender, EventArgs e)
        {
            string msgString = string.Empty;
            if (clbFeatureDefinitions.CheckedItems.Count == 1)
            {
                Feature feature = (Feature)clbFeatureDefinitions.CheckedItems[0];

                if (MessageBox.Show("This will forcefully uninstall the " + clbFeatureDefinitions.CheckedItems.Count +
                    " selected feature definition(s) from the Farm. Continue ?",
                    "Warning",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    if (MessageBox.Show("Before uninstalling a feature, it should be deactivated everywhere in the farm. " +
                        "Should the Feature be removed from everywhere in the farm before it gets uninstalled?",
                        "Please Select",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        // iterate through the farm to remove the feature
                        if (feature.Scope == SPFeatureScope.ScopeInvalid)
                        {
                            RemoveInvalidFeature formScopeUnclear = new RemoveInvalidFeature(this, feature.Id);
                            formScopeUnclear.Show();
                            return;

                        }
                        else
                        {
                            removeFeaturesWithinFarm(feature.Id, feature.Scope);
                        }

                    }

                    Cursor.Current = Cursors.WaitCursor;

                    UninstallSelectedFeatureDefinitions(farmFeatureDefinitionsManager, clbFeatureDefinitions.CheckedItems);

                    Cursor.Current = Cursors.Default;


                }
            }
            else
            {
                MessageBox.Show("Please select exactly 1 feature.");
            }
        }

        #endregion

        #region Feature removal (SiteCollection and SPWeb)

        /// <summary>triggers removeFeaturesFromCurrentLists</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRemoveFromList_Click(object sender, EventArgs e)
        {
            removeSPWebFeaturesFromCurrentList();
        }

        /// <summary>Removes selected features from the current list only</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void removeSPWebFeaturesFromCurrentList()
        {
            if (clbSPSiteFeatures.CheckedItems.Count > 0)
            {
                MessageBox.Show("Please uncheck all SiteCollection scoped features. Action canceled.",
                    "No SiteCollection scoped Features must be checked");
                return;
            }
            if (clbSPWebFeatures.CheckedItems.Count > 0)
            {
                int featuresRemoved = 0;
                string msgString;
                msgString = "This will force remove/deactivate the selected Web scoped Feature(s) from the selected Site(SPWeb) only. Continue ?";

                if (MessageBox.Show(msgString, "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Cursor.Current = Cursors.WaitCursor;

                    featuresRemoved = DeleteSelectedFeatures(siteFeatureManager, clbSPSiteFeatures.CheckedItems);
                    featuresRemoved = DeleteSelectedFeatures(webFeatureManager, clbSPWebFeatures.CheckedItems);

                    Cursor.Current = Cursors.Default;

                    msgString = "Done. Please refresh the feature list, when all features are removed!";
                    logDateMsg(msgString);
                }
            }
            else
            {
                MessageBox.Show(NOFEATURESELECTED);
                txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - " + NOFEATURESELECTED);
            }
        }

        /// <summary>Removes selected features from the current SiteCollection only</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRemoveFromSiteCollection_Click(object sender, EventArgs e)
        {

            string msgString = string.Empty;

            if ((clbSPSiteFeatures.CheckedItems.Count > 0) || (clbSPWebFeatures.CheckedItems.Count > 0))
            {
                int featuresRemoved = 0;
                if (clbSPSiteFeatures.CheckedItems.Count == 0)
                {
                    // normal removal of SiteColl Features from one site collection
                    removeSPWebFeaturesFromCurrentList();
                    return;
                }
                else
                {
                    // check which message, because there should be a warning, 
                    // when spweb and spsite features are mixed up in one action
                    if (clbSPWebFeatures.CheckedItems.Count == 0)
                    {
                        // only remove SPWeb features from a site collection
                        msgString = "This will force remove/deactivate the selected Site scoped Feature(s) from the selected SiteCollection. Continue ?";
                    }
                    else
                    {
                        msgString = "This will force remove/deactivate all selected Features (Scoped Site AND Web!) from " +
                            "the complete SiteCollection. Please be aware, that the selected Web Scoped Features " +
                            "will be removed from each Site within the currently selected Site Collection!! " +
                            "It is recommended to select only one Feature for Multisite deletion! Continue?";
                    }
                    if (MessageBox.Show(msgString, "Warning - Multi Site Deletion!", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        Cursor.Current = Cursors.WaitCursor;

                        // the site collection features can be easily removed same as before
                        featuresRemoved += DeleteSelectedFeatures(siteFeatureManager, clbSPSiteFeatures.CheckedItems);

                        // the web features need a special treatment
                        foreach (Feature checkedFeature in clbSPWebFeatures.CheckedItems)
                        {
                            featuresRemoved += removeWebFeaturesWithinSiteCollection(m_CurrentSite, checkedFeature.Id);
                        }

                        Cursor.Current = Cursors.Default;

                        removeReady(featuresRemoved);
                    }
                }
            }

            else
            {
                MessageBox.Show(NOFEATURESELECTED);
                txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - " + NOFEATURESELECTED);
            }

        }
        /// <summary>Removes selected features from the current Web Application only</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRemoveFromWebApp_Click(object sender, EventArgs e)
        {
            string msgString = string.Empty;
            int featuresRemoved = 0;
            int featuresSelected = clbSPSiteFeatures.CheckedItems.Count + clbSPWebFeatures.CheckedItems.Count;

            if (featuresSelected > 0)
            {
                msgString = "The " + featuresSelected + " selected Feature(s) " +
                    "will be removed/deactivated from the complete web application: " + m_CurrentWebApp + ". Continue?";

                if (MessageBox.Show(msgString, "Warning - Multi Site Deletion!", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Cursor.Current = Cursors.WaitCursor;

                    // remove web scoped features from web application
                    featuresRemoved += removeAllSelectedFeatures(clbSPSiteFeatures.CheckedItems, SPFeatureScope.WebApplication, SPFeatureScope.Site);

                    // remove SiteCollection scoped features from web application
                    featuresRemoved += removeAllSelectedFeatures(clbSPWebFeatures.CheckedItems, SPFeatureScope.WebApplication, SPFeatureScope.Web);


                    Cursor.Current = Cursors.Default;

                    removeReady(featuresRemoved);
                }
            }
            else
            {
                MessageBox.Show(NOFEATURESELECTED);
                txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - " + NOFEATURESELECTED);
            }


        }

        /// <summary>Removes selected features from the whole Farm</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRemoveFromFarm_Click(object sender, EventArgs e)
        {
            string msgString = string.Empty;
            int featuresRemoved = 0;
            int featuresSelected = clbSPSiteFeatures.CheckedItems.Count + clbSPWebFeatures.CheckedItems.Count;

            if (featuresSelected > 0)
            {
                msgString = "The " + featuresSelected + " selected Feature(s) " +
                    "will be removed/deactivated in the complete Farm! Continue?";

                if (MessageBox.Show(msgString, "Warning - Multi Site Deletion!", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Cursor.Current = Cursors.WaitCursor;

                    // remove web scoped features from web application
                    featuresRemoved += removeAllSelectedFeatures(clbSPSiteFeatures.CheckedItems, SPFeatureScope.Farm, SPFeatureScope.Site);

                    // remove SiteCollection scoped features from web application
                    featuresRemoved += removeAllSelectedFeatures(clbSPWebFeatures.CheckedItems, SPFeatureScope.Farm, SPFeatureScope.Web);


                    Cursor.Current = Cursors.Default;

                    removeReady(featuresRemoved);
                }
            }
            else
            {
                MessageBox.Show(NOFEATURESELECTED);
                txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - " + NOFEATURESELECTED);
            }
        }




        #endregion

        #region Feature Activation

        private void featureActivater(SPFeatureScope activationScope)
        {
            int featuresActivated = 0;
            string msgString = string.Empty;

            int checkedItems = clbFeatureDefinitions.CheckedItems.Count;

            foreach (Feature checkedFeature in clbFeatureDefinitions.CheckedItems)
            {
                switch (activationScope)
                {
                    case SPFeatureScope.Farm:
                        msgString = "Do you really want to activate the checked feature: '" + checkedFeature.ToString() + "' in the whole farm?";

                        if (MessageBox.Show(msgString, "Warning!", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            featuresActivated += featureActivateInSPFarm(checkedFeature.Id, checkedFeature.Scope);
                        }
                        break;

                    case SPFeatureScope.WebApplication:
                        if (m_CurrentWebApp == null)
                        {
                            MessageBox.Show("No Web Application selected.");
                            return;
                        }
                        msgString = "Do you really want to activate the selected feature: '" + checkedFeature.ToString() + "' in the web app '" +
                            m_CurrentWebApp.Name.ToString() + "'?";
                        if (MessageBox.Show(msgString, "Warning!", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            featuresActivated += featureActivateInSPWebApp(m_CurrentWebApp, checkedFeature.Id, checkedFeature.Scope);
                        }
                        break;

                    case SPFeatureScope.Site:
                        if (m_CurrentSite == null)
                        {
                            MessageBox.Show("No SiteCollection selected.");
                            return;
                        }
                        msgString = "The feature: <" + checkedFeature.ToString() + "> will be activated everywhere in Site Collection <" +
                            m_CurrentSite.Url.ToString() + ">?";
                        if (MessageBox.Show(msgString, "Please Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            featuresActivated += featureActivateInSPSite(m_CurrentSite, checkedFeature.Id, checkedFeature.Scope);
                        }
                        break;

                    case SPFeatureScope.Web:
                        if (m_CurrentWeb == null)
                        {
                            MessageBox.Show("No Site (SPWeb) selected.");
                            return;
                        }
                        if (checkedFeature.Scope != SPFeatureScope.Web)
                            goto default;
                        msgString = "The feature: <" + checkedFeature.ToString() + "> will be activated in the Site <" +
                             m_CurrentWeb.Url.ToString() + ">?";
                        if (MessageBox.Show(msgString, "Please Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            featuresActivated += featureActivateInSPWeb(m_CurrentWeb, checkedFeature.Id);
                        }
                        break;

                    default:

                        msgString = "Error, selected feature can't be activated - " + checkedFeature.ToString();
                        MessageBox.Show(msgString, "Warning!");
                        logDateMsg(msgString);
                        break;

                }

            }
            msgString = featuresActivated.ToString() + " Feature(s) were/was activated.";
            MessageBox.Show(msgString);
            logDateMsg(msgString);

        }

        /// <summary>activates a SPWeb feature in a SPWeb site</summary>
        /// <param name="web"></param>
        /// <param name="featureID"></param>
        /// <returns></returns>
        public int featureActivateInSPWeb(SPWeb web, Guid featureID)
        {
            int featuresActivated = 0;

            string msgString;
            try
            {
                if (!(web.Features[featureID] is SPFeature))
                {
                    web.Features.Add(featureID);
                    featuresActivated++;
                }
            }
            catch
            {
                msgString = "Error, selected feature can't be activated - " + featureID.ToString() + " in Web: '" + web.Name.ToString() + " - " + web.Url.ToString() + "'.";
                MessageBox.Show(msgString, "Warning!");
                logDateMsg(msgString);
                return 0;
            }
            msgString = featuresActivated + " Feature added in Site" + web.Url.ToString();
            logDateMsg(msgString);
            return featuresActivated;
        }

        /// <summary>activates features in a SiteCollection</summary>
        /// <param name="site"></param>
        /// <param name="featureID"></param>
        /// <param name="featureScope"></param>
        /// <returns></returns>
        public int featureActivateInSPSite(SPSite site, Guid featureID, SPFeatureScope featureScope)
        {
            int featuresActivated = 0;
            string msgString = string.Empty;

            if (featureScope == SPFeatureScope.Web)
            {
                // activate web feature in every SPWeb in this SPSite
                foreach (SPWeb web in site.AllWebs)
                {
                    featuresActivated += featureActivateInSPWeb(web, featureID);
                    if (web != null)
                    {
                        web.Dispose();
                    }
                }
            }
            else
            {
                if (featureScope == SPFeatureScope.Site)
                {
                    // activate Site scoped feature in this SPSite
                    try
                    {
                        if (!(site.Features[featureID] is SPFeature))
                        {
                            site.Features.Add(featureID);
                            featuresActivated += 1;
                        }
                    }
                    catch
                    {
                        msgString = "Error, selected feature can't be activated - " + featureID.ToString() + " in SiteCollection: '" + site.Url.ToString() + "'.";
                        MessageBox.Show(msgString, "Warning!");
                        logDateMsg(msgString);
                    }
                }

                else
                {
                    msgString = "Error, Scope " + featureScope + " not allowed for Activation in SiteCollection";
                    MessageBox.Show(msgString, "Warning!");
                    logDateMsg(msgString);
                    return 0;
                }
            }
            msgString = featuresActivated + " Features added in SiteCollection" + site.Url.ToString();
            logDateMsg(msgString);

            return featuresActivated;
        }

        /// <summary>activate all features in a web application</summary>
        /// <param name="webApp"></param>
        /// <param name="featureID"></param>
        /// <param name="featureScope"></param>
        /// <returns></returns>
        public int featureActivateInSPWebApp(SPWebApplication webApp, Guid featureID, SPFeatureScope featureScope)
        {
            string msgString;
            int featuresActivated = 0;

            switch (featureScope)
            {
                // Web Application Scoped Feature Activation   
                case SPFeatureScope.WebApplication:

                    // activate web app scoped feature in the web application
                    try
                    {
                        if (!(webApp.Features[featureID] is SPFeature))
                        {

                            webApp.Features.Add(featureID);
                            featuresActivated += 1;
                        }
                    }
                    catch
                    {
                        msgString = "Error, selected feature can't be activated - in WebApp: '" + webApp.Name.ToString() + "'.";
                        MessageBox.Show(msgString, "Warning!");
                        logDateMsg(msgString);
                    }


                    break;

                #region Iterate through lower scopes
                // Site Scoped Feature Activation   
                case SPFeatureScope.Site:
                    // activate site feature in every SPSit in this Web Application
                    foreach (SPSite site in webApp.Sites)
                    {
                        featuresActivated += featureActivateInSPSite(site, featureID, featureScope);

                        if (site != null)
                            site.Dispose();
                    }

                    break;

                // Web Scoped Feature Activation   
                case SPFeatureScope.Web:

                    // activate web feature in every SPWeb in this Web Application
                    foreach (SPSite site in webApp.Sites)
                    {
                        foreach (SPWeb web in site.AllWebs)
                        {
                            featuresActivated += featureActivateInSPWeb(web, featureID);
                            if (web != null)
                            {
                                web.Dispose();
                            }
                        }
                        if (site != null)
                        {
                            site.Dispose();
                        }
                    }
                    break;
                #endregion

                default:
                    msgString = "Error, Scope " + featureScope + " not allowed for Activation in Web Application";
                    MessageBox.Show(msgString, "Warning!");
                    logDateMsg(msgString);
                    break;
            }
            return featuresActivated;

        }

        /// <summary>activate features in the whole farm</summary>
        /// <param name="featureID"></param>
        /// <param name="featureScope"></param>
        /// <returns></returns>
        public int featureActivateInSPFarm(Guid featureID, SPFeatureScope featureScope)
        {
            string msgString;
            int featuresActivated = 0;

            // all the content WebApplications 
            SPWebApplicationCollection webApplicationCollection = SPWebService.ContentService.WebApplications;

            switch (featureScope)
            {
                case SPFeatureScope.Farm:

                    // activate farm scoped features to the farm
                    try
                    {
                        if (!(SPWebService.ContentService.Features[featureID] is SPFeature))
                        {
                            SPWebService.ContentService.Features.Add(featureID);
                            featuresActivated += 1;
                        }
                    }
                    catch
                    {
                        msgString = "Error, selected feature can't be activated - " + featureID.ToString() + " on Farm level.";
                        MessageBox.Show(msgString, "Warning!");
                        logDateMsg(msgString);
                    }
                    break;

                #region Iterate through lower scopes
                // Web Application Scoped Feature Activation            
                case SPFeatureScope.WebApplication:


                    // activate web app feature in every Web Application
                    foreach (SPWebApplication webApp in webApplicationCollection)
                    {
                        featuresActivated += featureActivateInSPWebApp(webApp, featureID, featureScope);
                    }


                    break;



                // SPSite Scoped Feature Activation
                case SPFeatureScope.Site:
                    foreach (SPWebApplication webApp in webApplicationCollection)
                    {

                        // activate site feature in every SPSite in this Web Application
                        foreach (SPSite site in webApp.Sites)
                        {
                            featuresActivated += featureActivateInSPSite(site, featureID, featureScope);

                            if (site != null)
                                site.Dispose();
                        }
                    }
                    break;

                // Web Scoped Feature Activation
                case SPFeatureScope.Web:

                    foreach (SPWebApplication webApp in webApplicationCollection)
                    {

                        // activate web feature in every SPWeb in this Web Application
                        foreach (SPSite site in webApp.Sites)
                        {
                            foreach (SPWeb web in site.AllWebs)
                            {
                                featuresActivated += featureActivateInSPWeb(web, featureID);
                                if (web != null)
                                {
                                    web.Dispose();
                                }
                            }
                            if (site != null)
                            {
                                site.Dispose();
                            }
                        }
                    }
                    break;

                #endregion

                default:
                    msgString = "Error, Scope " + featureScope + " not allowed for Activation in Web Application";
                    MessageBox.Show(msgString, "Warning!");
                    logDateMsg(msgString);
                    break;
            }
            return featuresActivated;

        }


        #endregion

        #region Helper Methods


        /// <summary>gets all the features from the selected Web Site and Site Collection</summary>
        private void getFeatures()
        {
            Cursor.Current = Cursors.WaitCursor;

            txtResult.Clear();
            clbSPSiteFeatures.Items.Clear();
            clbSPWebFeatures.Items.Clear();

            //using (SPSite site = new SPSite(txtUrl.Text))
            using (SPSite site = m_CurrentSite)
            {
                siteFeatureManager = new FeatureManager(site.Url);
                siteFeatureManager.AddFeatures(site.Features, SPFeatureScope.Site);

                //using (SPWeb web = site.OpenWeb())
                using (SPWeb web = m_CurrentWeb)
                {
                    webFeatureManager = new FeatureManager(web.Url);
                    webFeatureManager.AddFeatures(web.Features, SPFeatureScope.Web);
                }
            }

            // sort the features list
            siteFeatureManager.Features.Sort();
            clbSPSiteFeatures.Items.AddRange(siteFeatureManager.Features.ToArray());
            txtResult.Text += BuildFeatureLog(siteFeatureManager.Url, siteFeatureManager.Features);

            // sort the features list
            webFeatureManager.Features.Sort();
            clbSPWebFeatures.Items.AddRange(webFeatureManager.Features.ToArray());
            txtResult.Text += BuildFeatureLog(webFeatureManager.Url, webFeatureManager.Features);

            // enables the removal buttons
            // removeBtnEnabled(true);

            Cursor.Current = Cursors.Default;

            // commented out, was too annoying
            // MessageBox.Show("Done.");
            txtResult.AppendText(DateTime.Now.ToString(DATETIMEFORMAT) + " - " + "Feature list updated." + Environment.NewLine);
        }

        /// <summary>write the log in the form, when features are loaded</summary>
        /// <param name="location"></param>
        /// <param name="features"></param>
        /// <returns></returns>
        private string BuildFeatureLog(string location, List<Feature> features)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(location);
            sb.AppendFormat("Features counted: {0}", features.Count);
            sb.AppendLine();

            foreach (Feature feature in features)
            {
                sb.AppendLine(feature.ToString());
            }

            // sb.AppendLine();

            return sb.ToString();
        }

        private void logFeatureSelected()
        {
            if (clbSPSiteFeatures.CheckedItems.Count + clbSPWebFeatures.CheckedItems.Count > 0)
            {

                // enable all remove buttons
                removeBtnEnabled(true);

                txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - " + "Feature selection changed:" + Environment.NewLine);
                if (clbSPSiteFeatures.CheckedItems.Count > 0)
                {
                    foreach (Feature checkedFeature in clbSPSiteFeatures.CheckedItems)
                    {
                        txtResult.AppendText(checkedFeature.ToString() + ", Scope: Site" + Environment.NewLine);
                    }
                }

                if (clbSPWebFeatures.CheckedItems.Count > 0)
                {
                    foreach (Feature checkedFeature in clbSPWebFeatures.CheckedItems)
                    {
                        txtResult.AppendText(checkedFeature.ToString() + ", Scope: Web" + Environment.NewLine);
                    }
                }
            }
            else
            {
                // disable all remove buttons
                removeBtnEnabled(false);
            }

        }

        private void logFeatureDefinitionSelected()
        {
            if (clbFeatureDefinitions.CheckedItems.Count > 0)
            {

                // enable all FeatureDef buttons
                featDefBtnEnabled(true);


                txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - " + "Feature Definition selection changed:" + Environment.NewLine);

                foreach (Feature checkedFeature in clbFeatureDefinitions.CheckedItems)
                {
                    txtResult.AppendText(checkedFeature.ToString() + Environment.NewLine);
                }
            }
            else
            {
                // disable all FeatureDef buttons
                featDefBtnEnabled(false);

            }
        }

        /// <summary>Delete a collection of SiteCollection or Web Features forcefully in one site</summary>
        /// <param name="manager"></param>
        /// <param name="checkedListItems"></param>
        private int DeleteSelectedFeatures(FeatureManager manager, CheckedListBox.CheckedItemCollection checkedListItems)
        {
            int removedFeatures = 0;
            foreach (Feature checkedFeature in checkedListItems)
            {
                manager.ForceRemoveFeature(checkedFeature.Id);
                removedFeatures++;
            }
            return removedFeatures;
        }


        /// <summary>Delete a collection of SiteCollection or Web Features forcefully in one site</summary>
        /// <param name="manager"></param>
        /// <param name="checkedListItems"></param>
        private int removeAllSelectedFeatures(CheckedListBox.CheckedItemCollection checkedListItems, SPFeatureScope deletionScope, SPFeatureScope featureScope)
        {
            int removedFeatures = 0;
            foreach (Feature checkedFeature in checkedListItems)
            {
                switch (deletionScope)
                {

                    case SPFeatureScope.Farm:
                        removedFeatures += removeFeaturesWithinFarm(checkedFeature.Id, featureScope);
                        break;
                    case SPFeatureScope.WebApplication:
                        removedFeatures += removeFeaturesWithinWebApp(m_CurrentWebApp, checkedFeature.Id, featureScope);
                        break;

                    // only relevant for web scoped features
                    case SPFeatureScope.Site:
                        if (featureScope == SPFeatureScope.Site) goto default;
                        removedFeatures += removeWebFeaturesWithinSiteCollection(m_CurrentSite, checkedFeature.Id);
                        break;

                    default:
                        txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) +
                            " - no features removed, deletion scope defined wrong!");
                        break;
                }

            }
            return removedFeatures;
        }

        /// <summary>remove all Web scoped features from a SiteCollection</summary>
        /// <param name="site"></param>
        /// <param name="featureID"></param>
        /// <returns>number of deleted features</returns>
        private int removeWebFeaturesWithinSiteCollection(SPSite site, Guid featureID)
        {
            int removedFeatures = 0;
            int scannedThrough = 0;
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {

                foreach (SPWeb web in site.AllWebs)
                {
                    try
                    {
                        //forcefully remove the feature
                        if (web.Features[featureID].DefinitionId != null)
                        {
                            web.Features.Remove(featureID, true);
                            removedFeatures++;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        scannedThrough++;
                        if (web != null)
                        {
                            web.Dispose();
                        }
                    }
                }
                string msgString = removedFeatures + " Web Scoped Features removed in the SiteCollection " + site.Url.ToString() + ". " + scannedThrough + " sites/subsites were scanned.";
                txtResult.AppendText(DateTime.Now.ToString(DATETIMEFORMAT) + " -   SiteColl - " + msgString + Environment.NewLine);


            });
            return removedFeatures;
        }

        /// <summary>remove all features within a web application, if feature is web scoped, different method is called</summary>
        /// <param name="webApp"></param>
        /// <param name="featureID"></param>
        /// <param name="trueForSPWeb"></param>
        /// <returns>number of deleted features</returns>
        private int removeFeaturesWithinWebApp(SPWebApplication webApp, Guid featureID, SPFeatureScope featureScope)
        {
            int removedFeatures = 0;
            int scannedThrough = 0;
            string msgString;

            msgString = "Removing Feature '" + featureID.ToString() + "' from Web Application: '" + webApp.Name.ToString() + "'.";
            txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " -  WebApp - " + msgString + Environment.NewLine);

            SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    if (featureScope == SPFeatureScope.WebApplication)
                    {
                        try
                        {
                            webApp.Features.Remove(featureID, true);
                            removedFeatures++;
                        }
                        catch
                        {
                        }
                    }
                    else
                    {

                        foreach (SPSite site in webApp.Sites)
                        {
                            try
                            {
                                if (featureScope == SPFeatureScope.Web)
                                {
                                    removedFeatures += removeWebFeaturesWithinSiteCollection(site, featureID);
                                }
                                else
                                {
                                    //forcefully remove the feature
                                    site.Features.Remove(featureID, true);
                                    removedFeatures += 1;

                                }
                                scannedThrough++;
                            }
                            catch
                            {
                            }
                            finally
                            {
                                if (site != null)
                                {
                                    site.Dispose();
                                }

                            }

                        }
                    }

                });
            msgString = removedFeatures + " Features removed in the Web Application. " + scannedThrough + " SiteCollections were scanned.";
            txtResult.AppendText(DateTime.Now.ToString(DATETIMEFORMAT) + " -  WebApp - " + msgString + Environment.NewLine);

            return removedFeatures;
        }

        /// <summary>removes the defined feature within a complete farm</summary>
        /// <param name="featureID"></param>
        /// <param name="trueForSPWeb"></param>
        /// <returns>number of deleted features</returns>
        public int removeFeaturesWithinFarm(Guid featureID, SPFeatureScope featureScope)
        {
            int removedFeatures = 0;
            int scannedThrough = 0;
            string msgString;

            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                msgString = "Removing Feature '" + featureID.ToString() + ", Scope: " + featureScope.ToString() + "' from the Farm.";
                txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - Farm - " + msgString + Environment.NewLine);
                if (featureScope == SPFeatureScope.Farm)
                {
                    try
                    {
                        SPWebService.ContentService.Features.Remove(featureID, true);
                        removedFeatures++;
                        txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - Farm - Feature successfully removed. " + Environment.NewLine);

                    }
                    catch
                    {
                        txtResult.AppendText(Environment.NewLine + DateTime.Now.ToString(DATETIMEFORMAT) + " - Farm - The Farm Scoped feature '" + featureID.ToString() + "' was not found. " + Environment.NewLine);

                    }
                }
                else
                {

                    // all the content WebApplications 
                    SPWebApplicationCollection webApplicationCollection = SPWebService.ContentService.WebApplications;

                    //  administrative WebApplications 
                    // SPWebApplicationCollection webApplicationCollection = SPWebService.AdministrationService.WebApplications;

                    foreach (SPWebApplication webApplication in webApplicationCollection)
                    {

                        removedFeatures += removeFeaturesWithinWebApp(webApplication, featureID, featureScope);
                        scannedThrough++;
                    }
                }
                msgString = removedFeatures + " Features removed in the Farm. " + scannedThrough + " Web Applications were scanned.";
                txtResult.AppendText(DateTime.Now.ToString(DATETIMEFORMAT) + " - Farm - " + msgString + Environment.NewLine);

            });
            return removedFeatures;
        }


        /// <summary>Uninstall a collection of Farm Feature Definitions forcefully</summary>
        /// <param name="manager"></param>
        /// <param name="checkedListItems"></param>
        private void UninstallSelectedFeatureDefinitions(FeatureManager manager, CheckedListBox.CheckedItemCollection checkedListItems)
        {
            foreach (Feature checkedFeature in checkedListItems)
            {
                manager.ForceUninstallFeatureDefinition(checkedFeature.Id);
            }
        }


        /// <summary>enables or disables all buttons for feature removal</summary>
        /// <param name="enabled">true = enabled, false = disabled</param>
        private void removeBtnEnabled(bool enabled)
        {
            btnRemoveFromList.Enabled = enabled;
            btnRemoveFromSiteCollection.Enabled = enabled;
            btnRemoveFromWebApp.Enabled = enabled;
            btnRemoveFromFarm.Enabled = enabled;
        }


        /// <summary>enables or disables all buttons for feature definition administration</summary>
        /// <param name="enabled">true = enabled, false = disabled</param>
        private void featDefBtnEnabled(bool enabled)
        {
            btnUninstFDef.Enabled = enabled;
            btnActivateSPWeb.Enabled = enabled;
            btnActivateSPSite.Enabled = enabled;
            btnActivateSPWebApp.Enabled = enabled;
            btnActivateSPFarm.Enabled = enabled;
            btnFindActivatedFeature.Enabled = enabled;
        }

        private void removeReady(int featuresRemoved)
        {
            string msgString;
            msgString = featuresRemoved.ToString() + " Features were removed. Please 'Reload Web Applications'!";
            MessageBox.Show(msgString);
            logDateMsg(msgString);
        }

        /// <summary>searches for faulty features and provides the option to remove them</summary>
        /// <param name="features">SPFeatureCollection, the container for the features</param>
        /// <param name="scope">is needed, in case a feature is found, so that it can be deleted</param>
        /// <returns></returns>
        private bool findFaultyFeatureInCollection(SPFeatureCollection features, SPFeatureScope scope)
        {
            string dummy;
            Guid faultyID = Guid.Empty;
            string msgString = string.Empty;

            if (features != null)
            {
                foreach (SPFeature feature in features)
                {
                    string parentString;
                    try
                    {
                        // a feature activated somewhere with no manifest file available causes
                        // an error when asking for the DisplayName
                        // If this happens, we found a faulty feature
                        faultyID = feature.DefinitionId;
                        dummy = feature.Definition.DisplayName;
                    }
                    catch
                    {
                        if (features[faultyID].Parent is SPWeb)
                        {
                            parentString = "Scope:Web, " + ((SPWeb)features[faultyID].Parent).Url.ToString();
                        }
                        else
                        {
                            parentString = features[faultyID].Parent.ToString();
                        }

                        msgString = "Faulty Feature found! Id: '" + faultyID.ToString() + Environment.NewLine +
                            "Found in " + parentString + ". Should it be removed from the farm?";
                        logDateMsg(msgString);
                        if (MessageBox.Show(msgString, "Success! Please Decide",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            removeFeaturesWithinFarm(faultyID, scope);
                        }

                        return true;
                    }
                }

            }
            else
            {
                txtResult.AppendText(DateTime.Now.ToString(DATETIMEFORMAT) + " - ERROR: Feature Collection was empty!" + Environment.NewLine);
            }
            faultyID = Guid.Empty;
            return false;
        }

        #endregion
        #region Error & Logging Methods

        protected void ReportError(string msg)
        {
            ReportError(msg, "Error");
        }
        protected void ReportError(string msg, string caption)
        {
            // TODO - be nice to have an option to suppress message boxes
            MessageBox.Show(msg, caption);
        }

        protected string FormatSiteException(SPSite site, Exception exc, string msg)
        {
            msg += " on site " + site.ServerRelativeUrl + " (ContentDB: " + site.ContentDatabase.Name + ")";
            if (IsSimpleAccessDenied(exc))
            {
                msg += " (dbOwner rights recommended on contentdb)";
            }
            return msg;
        }

        protected void logException(Exception exc, string msg)
        {
            logDateMsg(msg + " -- " + DescribeException(exc));
        }

        protected bool IsSimpleAccessDenied(Exception exc)
        {
            return (exc is System.UnauthorizedAccessException && exc.InnerException == null);
        }

        protected string DescribeException(Exception exc)
        {
            if (IsSimpleAccessDenied(exc))
            {
                return "Access is Denied";
            }
            StringBuilder txt = new StringBuilder();
            while (exc != null)
            {
                if (txt.Length > 0) txt.Append(" =++= ");
                txt.Append(exc.Message);
                exc = exc.InnerException;
            }
            return txt.ToString();
        }

        protected void logDateMsg(string msg)
        {
            logTxt(DateTime.Now.ToString(DATETIMEFORMAT) + " - " + msg + Environment.NewLine);
        }

        /// <summary>adds log string to the logfile</summary>
        /// <param name="logtext"></param>
        public void logTxt(string logtext)
        {
            this.txtResult.AppendText(logtext);
        }

        #endregion

        #region Feature lists, WebApp, SiteCollection and SPWeb list set up

        /// <summary>trigger load of web application list</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnListWebApplications_Click(object sender, EventArgs e)
        {
            loadWebAppList();
        }

        /// <summary>populate the web application list</summary>
        private void loadWebAppList()
        {
            Cursor.Current = Cursors.WaitCursor;
            listWebApplications.Items.Clear();
            listSiteCollections.Items.Clear();
            listSites.Items.Clear();
            clbSPSiteFeatures.Items.Clear();
            clbSPWebFeatures.Items.Clear();
            removeBtnEnabled(false);

            if (SPWebService.ContentService != null)
            {
                foreach (SPWebApplication webApp in SPWebService.ContentService.WebApplications)
                {
                    listWebApplications.Items.Add(webApp.Name);
                }
            }
            else
            {
                listWebApplications.Items.Add("SPWebService.ContentService == null");
            }

            if (listWebApplications.Items.Count > 0)
            {
                listSiteCollections.Enabled = true;
            }
            else
            {
                listSiteCollections.Enabled = false;
            }
            Cursor.Current = Cursors.Default;
        }

        /// <summary>Update SiteCollections list when a user changes the selection in Web Application list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listWebApplications_SelectedIndexChanged(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                listSiteCollections.Items.Clear();
                listSites.Items.Clear();
                clbSPSiteFeatures.Items.Clear();
                clbSPWebFeatures.Items.Clear();
                removeBtnEnabled(false);

                if (listWebApplications.SelectedItem.ToString() != String.Empty)
                {
                    foreach (SPWebApplication webApp in SPWebService.ContentService.WebApplications)
                    {
                        if (webApp.Name == listWebApplications.SelectedItem.ToString())
                        {
                            m_CurrentWebApp = webApp;
                        }
                    }

                    foreach (SPSite site in m_CurrentWebApp.Sites)
                    {
                        listSiteCollections.Items.Add(site.Url);
                    }
                }

                // tabControl1.Enabled = false;
                // tabControl1.Visible = false;
                // listFeatures.Items.Clear();
                // listDetails.Items.Clear();
            }
            catch (Exception)
            {

            }
            Cursor.Current = Cursors.Default;
        }

        /// <summary>UI method to update the SPWeb list when a user changes the selection in site collection list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listSiteCollections_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listSiteCollections.SelectedIndex > -1)
            {
                Cursor.Current = Cursors.WaitCursor;
                //Clear the lists
                listSites.Items.Clear();
                clbSPSiteFeatures.Items.Clear();
                clbSPWebFeatures.Items.Clear();
                removeBtnEnabled(false);

                // tabControl1.Enabled = true;
                // tabControl1.Visible = true;

                //If there is only one site collection chosen, list the subsites.
                if (listSiteCollections.SelectedItems.Count == 1)
                {
                    m_CurrentSite = m_CurrentWebApp.Sites[listSiteCollections.SelectedIndices[0]];
                    try
                    {
                        foreach (SPWeb web in m_CurrentSite.AllWebs)
                        {
                            listSites.Items.Add(web.Name + " - " + web.Title + " - " + web.Url);
                        }
                    }
                    catch (Exception exc)
                    {
                        string msg = FormatSiteException(m_CurrentSite, exc, "Error enumerating webs");
                        ReportError(msg);
                        logException(exc, msg);
                    }

                    //List the features for the site.
                    //FillFeatureList();
                }
                Cursor.Current = Cursors.Default;
            }
            else
            {
                // tabControl1.Enabled = false;
                // tabControl1.Visible = false;
            }

        }

        /// <summary>UI method to load the SiteCollection Features and Site Features
        /// Handles the SelectedIndexChanged event of the listSites control.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listSites_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Make sure we have selected a web.
            if (listSites.SelectedIndex > -1)
            {
                m_CurrentWeb = m_CurrentSite.AllWebs[listSites.SelectedIndices[0]];

                //list all the features
                getFeatures();
            }
            else
            {
                //listSPLists.Enabled = false;
                m_CurrentWeb = null;
            }
        }

        private void clbSPSiteFeatures_SelectedIndexChanged(object sender, EventArgs e)
        {
            logFeatureSelected();
        }

        private void clbSPWebFeatures_SelectedIndexChanged(object sender, EventArgs e)
        {
            logFeatureSelected();
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            txtResult.Clear();
        }


        private void clbFeatureDefinitions_SelectedIndexChanged(object sender, EventArgs e)
        {
            logFeatureDefinitionSelected();
        }

        private void btnActivateSPWeb_Click(object sender, EventArgs e)
        {
            featureActivater(SPFeatureScope.Web);
        }

        private void btnActivateSPSite_Click(object sender, EventArgs e)
        {
            featureActivater(SPFeatureScope.Site);
        }

        private void btnActivateSPWebApp_Click(object sender, EventArgs e)
        {
            featureActivater(SPFeatureScope.WebApplication);
        }

        private void btnActivateSPFarm_Click(object sender, EventArgs e)
        {
            featureActivater(SPFeatureScope.Farm);
        }

  

        private void btnFindActivatedFeature_Click(object sender, EventArgs e)
        {
            string msgString = string.Empty;

            if (clbFeatureDefinitions.CheckedItems.Count == 1)
            {
                Feature feature = (Feature)clbFeatureDefinitions.CheckedItems[0];

                //first, Look in Farm
                if ((SPWebService.ContentService.Features[feature.Id] is SPFeature))
                {
                    msgString = "Farm Feature is activated in the Farm on farm level!";
                    MessageBox.Show(msgString);
                    logDateMsg(msgString);
                    return;
                }

                // iterate through web apps
                SPWebApplicationCollection webApplicationCollection = SPWebService.ContentService.WebApplications;

                foreach (SPWebApplication webApp in webApplicationCollection)
                {
                    // check web apps
                    if (webApp.Features[feature.Id] is SPFeature)
                    {
                        msgString = "Web App scoped Feature is activated in WebApp '" + webApp.Name.ToString() + "'!";
                        MessageBox.Show(msgString);
                        logDateMsg(msgString);
                        return;
                    }

                    try
                    {
                        foreach (SPSite site in webApp.Sites)
                        {
                            // check sites
                            if (site.Features[feature.Id] is SPFeature)
                            {
                                msgString = "Site Feature is activated in SiteCollection '" + site.Url.ToString() + "'!";
                                MessageBox.Show(msgString);
                                logDateMsg(msgString);
                                site.Dispose();
                                return;
                            }


                            foreach (SPWeb web in site.AllWebs)
                            {
                                // check webs
                                if (web.Features[feature.Id] is SPFeature)
                                {
                                    msgString = "Web scoped Feature is activated in Site '" + web.Url.ToString() + "'!";
                                    MessageBox.Show(msgString);
                                    logDateMsg(msgString);
                                    web.Dispose();
                                    return;
                                }
                                if (web != null)
                                {
                                    web.Dispose();
                                }
                            }
                            if (site != null)
                            {
                                site.Dispose();
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        msgString = "Exception attempting to enumerate sites of WebApp: " + webApp.Name;
                        logException(exc, msgString);
                    }
                }
                msgString = "Feature was not found activated in the farm.";
                MessageBox.Show(msgString);
                logDateMsg(msgString);

            }
            else
            {
                MessageBox.Show("Please select exactly 1 feature.");
            }
        }


        private void btnFindFaultyFeature_Click(object sender, EventArgs e)
        {


            string msgString = string.Empty;


            //first, Look in Farm
            try
            {
                if (findFaultyFeatureInCollection(SPWebService.ContentService.Features, SPFeatureScope.Farm))
                {
                    return;
                }
            }
            catch (Exception exc)
            {
                logException(exc, "Error finding faulty features in farm");
            }

            //check web applications
            try
            {
                foreach (SPWebApplication webApp in SPWebService.ContentService.WebApplications)
                {
                    try
                    {
                        if (findFaultyFeatureInCollection(webApp.Features, SPFeatureScope.WebApplication))
                        {
                            return;
                        }
                    }
                    catch (Exception exc)
                    {
                        logException(exc, "Enumerating features in web app " + webApp.Name);
                    }

                    try
                    {
                        // then check all site collections
                        foreach (SPSite site in webApp.Sites)
                        {
                            try
                            {
                                // check sites
                                if (findFaultyFeatureInCollection(site.Features, SPFeatureScope.Site))
                                {
                                    return;
                                }
                            }
                            catch (Exception exc)
                            {
                                logException(exc, "Exception checking features in site " + site.Url);
                            }


                            try
                            {
                                foreach (SPWeb web in site.AllWebs)
                                {
                                    try
                                    {
                                        // check webs
                                        if (findFaultyFeatureInCollection(web.Features, SPFeatureScope.Web))
                                        {
                                            return;
                                        }
                                    }
                                    catch (Exception exc)
                                    {
                                        logException(exc, "Exception checking features in web " + web.Url);
                                    }

                                    if (web != null)
                                    {
                                        web.Dispose();
                                    }
                                }
                            }
                            catch (Exception exc)
                            {
                                logException(exc, "Exception enumerating subwebs in site "
                                    + site.ServerRelativeUrl
                                    + " (ContentDb: " + site.ContentDatabase.Name + ")"
                                    );
                            }
                            if (site != null)
                            {
                                site.Dispose();
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        logException(exc, "Exception enumerating sites in web app " + webApp.Name);
                    }
                }
            }
            catch (Exception exc)
            {
                logException(exc, "Enumerating web applications");
            }
            msgString = "No Faulty Feature was found in the farm!";
            MessageBox.Show(msgString);
            logDateMsg(msgString);

        }

        private void FrmMain_Load(object sender, EventArgs e)
        {

        }

        #endregion
    }
}
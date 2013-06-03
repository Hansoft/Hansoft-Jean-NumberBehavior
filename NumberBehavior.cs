using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using HPMSdk;
using Hansoft.Jean.Behavior;
using Hansoft.ObjectWrapper;

namespace Hansoft.Jean.Behavior.NumberBehavior
{
    public class NumberBehavior : AbstractBehavior
    {
        string title;

        string projectName;
        Project project;
        EHPMReportViewType viewType;
        ProjectView projectView;
        bool hierarchical = false;
        string columnName;
        HPMProjectCustomColumnsColumn rankColumn;
        bool iOptionFound = false;
        bool lOptionFound = false;
        List<int> ignoreLevels = new List<int>();
        List<int> labelLevels = new List<int>();
        bool[] explicitIgnoreLevels;
        bool[] explicitLabelLevels;

        bool changeImpact = false;

        public NumberBehavior(XmlElement configuration) : base(configuration) 
        {
            projectName = GetParameter("HansoftProject");
            viewType = GetViewType(GetParameter("View"));
            columnName = GetParameter("ColumnName");
            string styleValue = GetParameter("NumberingStyle");
            if (styleValue.Trim().Equals("Hierarchical"))
                hierarchical = true;
            string labelValue = GetParameter("LabelLevels");
            if (!labelValue.Equals(string.Empty))
            {
                lOptionFound = true;
                foreach (string lv in labelValue.Split(new char[] { ',' }))
                    labelLevels.Add(Int32.Parse(lv));
            }
            string ignoreValue = GetParameter("IgnoreLevels");
            if (!ignoreValue.Equals(string.Empty))
            {
                iOptionFound = true;
                foreach (string iv in ignoreValue.Split(new char[] { ',' }))
                    ignoreLevels.Add(Int32.Parse(iv));
            }

            if (iOptionFound)
                explicitIgnoreLevels = BuildLevelArray(ignoreLevels);
            if (lOptionFound)
                explicitLabelLevels = BuildLevelArray(labelLevels);
            title = "NumberBehavior: " + configuration.InnerText;
        }

        public override string Title
        {
            get { return title; }
        }

        public override void Initialize()
        {
            project = HPMUtilities.FindProject(projectName); 
            if (project == null)
                throw new ArgumentException("Could not find project:" + projectName);
            if (viewType == EHPMReportViewType.AgileBacklog)
                projectView = project.ProductBacklog;
            else
                projectView = project.Schedule;

            rankColumn = projectView.GetCustomColumn(columnName);
            if (rankColumn == null)
                throw new ArgumentException("Could not find custom column:" + columnName);
            DoRenumber(projectView, 1, "");
        }

        // TODO: Subject to refactoring
        private EHPMReportViewType GetViewType(string viewType)
        {
            switch (viewType)
            {
                case ("Agile"):
                    return EHPMReportViewType.AgileMainProject;
                case ("Scheduled"):
                    return EHPMReportViewType.ScheduleMainProject;
                case ("Bugs"):
                    return EHPMReportViewType.AllBugsInProject;
                case ("Backlog"):
                    return EHPMReportViewType.AgileBacklog;
                default:
                    throw new ArgumentException("Unsupported View Type: " + viewType);

            }
        }

        private bool[] BuildLevelArray(List<int> levels)
        {
            bool[] explicitLevels = new bool[levels.Last()];
            for (int i = 0; i < explicitLevels.Length; i += 1)
                explicitLevels[i] = false;
            foreach (int level in levels)
                explicitLevels[level - 1] = true;
            return explicitLevels;
        }

        void DoRenumber(HansoftItem parent, int level, string levelPath)
        {
            bool labelLevel = true;
            if (lOptionFound)
            {
                if (level <= explicitLabelLevels.Length)
                    labelLevel = explicitLabelLevels[level - 1];
                else
                    labelLevel = false;
            }
            else if (iOptionFound)
            {
                if (level <= explicitIgnoreLevels.Length)
                    labelLevel = !explicitIgnoreLevels[level - 1];
                else
                    labelLevel = true;
            }
            if (labelLevel)
            {
                int rank = 1;
                foreach (Task child in parent.Children)
                {
                    string rankString;
                    if (!hierarchical)
                        rankString = rank.ToString();
                    else
                        rankString = levelPath + rank.ToString();
                    if (child.GetCustomColumnValue(rankColumn).ToString() != rankString)
                        child.SetCustomColumnValue(rankColumn, rankString);

                    rank += 1;
                }
            }
            else
            {
                foreach (Task child in parent.Children)
                {
                    if (child.GetCustomColumnValue(rankColumn).ToString() != string.Empty)
                        child.SetCustomColumnValue(rankColumn, string.Empty);
                }
            }

            int rank2 = 1;
            foreach (Task child in parent.Children)
            {
                string newLevelPath = levelPath + rank2.ToString() + ".";
                DoRenumber(child, level + 1, newLevelPath);
                rank2 += 1;
            }
        }

        public override void OnBeginProcessBufferedEvents(EventArgs e)
        {
            changeImpact = false;
        }

        public override void OnEndProcessBufferedEvents(EventArgs e)
        {
            if (BufferedEvents && changeImpact)
                DoRenumber(projectView, 1, "");
        }

        public override void OnTaskCreate(TaskCreateEventArgs e)
        {
            if (e.Data.m_ProjectID.m_ID == projectView.UniqueID.m_ID)
            {
                if (!BufferedEvents)
                    DoRenumber(projectView, 1, "");
                else
                    changeImpact = true;
            }
        }

        public override void OnTaskDelete(TaskDeleteEventArgs e)
        {
            if (!BufferedEvents)
                DoRenumber(projectView, 1, "");
            else
                changeImpact = true;
        }

        public override void OnTaskMove(TaskMoveEventArgs e)
        {
            if (e.Data.m_ProjectID.m_ID == projectView.UniqueID.m_ID)
            {
                if (!BufferedEvents)
                    DoRenumber(projectView, 1, "");
                else
                    changeImpact = true;
            }
        }
    }
}

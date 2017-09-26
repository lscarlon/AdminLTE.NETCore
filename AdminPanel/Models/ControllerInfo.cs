﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AdminPanel.Attributes;
using AdminPanel.Common;

namespace AdminPanel.Models
{
    public class ControllerInfo
    {
        public string FullName { get; set; }
        public string DisplayName { get; set; }
        public string DisplayImage { get; set; }
        public string ControllerName { get; set; }
        public List<ActionInfo> ControllerActions { get; set; }
        public TreeViewAttribute TreeViewSettings { get; set; }
        public TreeViewSettingsAttribute TreeViewSettings2 { get; set; }
    }

    public interface IControllerInformationRepository
    {
        List<ControllerInfo> GetAll();
    }

    // Dynamically get a list of the controllers and actions for the AdminLTE demo
    public class ControllerInformationRepository : IControllerInformationRepository
    {
        private List<ControllerInfo> _controllerInfo = new List<ControllerInfo>();

        public ControllerInformationRepository()
        {
            //TODO: Refactor to make more readable
            _controllerInfo = Assembly.GetEntryAssembly()
                .GetTypes()
                .AsEnumerable()
                .Where(type => typeof(Controller).IsAssignableFrom(type))
                .Where(type => type.GetTypeInfo().GetCustomAttribute<DisplayOrderAttribute>().DisplayOrder>=0) //Escludo i controller con DisplayOrder negativo
                .ToList()
                .OrderBy(t =>
                {
                    var orderby = (DisplayOrderAttribute)t.GetTypeInfo().GetCustomAttribute<DisplayOrderAttribute>();
                    if (orderby != null)
                        return orderby.DisplayOrder;
                    else
                        return 0;
                }
                )
            .Select(
            d => new
            {
                fullname = d.FullName,
                controllerName = CleanupControllerName(d.Name),
                displayImage = d.GetTypeInfo().GetCustomAttribute<DisplayImageAttribute>(),
                action_list = d.GetMethods().Where(method => method.IsPublic && method.IsDefined(typeof(DisplayActionMenuAttribute))),
                treeview = d.GetTypeInfo().GetCustomAttribute<TreeViewAttribute>(),
                treeviewsettings = d.GetTypeInfo().GetCustomAttribute<TreeViewSettingsAttribute>()
            }
            )
                .ToList()
                .Select(
                   a =>
                    new ControllerInfo()
                    {
                        ControllerName = a.controllerName,
                        FullName = a.fullname,
                        DisplayName = a.controllerName.SeparatePascalCase(),
                        DisplayImage = a.displayImage.DisplayImage,
                        TreeViewSettings = a.treeview,
                        TreeViewSettings2 = a.treeviewsettings,
                        //Actions
                        ControllerActions = a.action_list.Select(act => new Models.ActionInfo()
                        {
                            ActionName = act.Name,
                            DisplayName = act.Name.SeparatePascalCase(),
                            DisplayImage = act.GetCustomAttributes<DisplayImageAttribute>().FirstOrDefault().DisplayImage, //Will generate an exception if the attribute is not defined.
                            ScriptAfterPartialView = act.GetCustomAttributes<ScriptAfterPartialViewAttribute>().FirstOrDefault().ScriptToRun, //Will generate an exception if the attribute is not defined.
                            TreeViewSettings = act.GetCustomAttributes<TreeViewAttribute>().FirstOrDefault(),
                            TreeViewSettings2 = act.GetCustomAttributes<TreeViewSettingsAttribute>().FirstOrDefault()
                        }).ToList()
                    }
            ).ToList();

        }

        private string CleanupControllerName(string controllerName)
        {
            return controllerName.Replace("Controller", "");
        }

        public List<ControllerInfo> GetAll()
        {
            return _controllerInfo;
        }
    }


}

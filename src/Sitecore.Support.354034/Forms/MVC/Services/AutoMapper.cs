namespace Sitecore.Support.Forms.Mvc.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Sitecore.Diagnostics;
    using Sitecore.Form.Core.Utility;
    using Sitecore.Forms.Core.Data;
    using Sitecore.Forms.Mvc.Helpers;
    using Sitecore.Forms.Mvc.Interfaces;
    using Sitecore.Forms.Mvc.Reflection;
    using Sitecore.Forms.Mvc.ViewModels;
    using Sitecore.Mvc.Extensions;
    using Sitecore.WFFM.Abstractions.Data;
    using Sitecore.Forms.Mvc.Services;

    public class AutoMapper : IAutoMapper<IFormModel, FormViewModel>
    {
        public FormViewModel GetView(IFormModel formModel)
        {
            Assert.ArgumentNotNull(formModel, "formModel");

            var formViewModel = new FormViewModel
            {
                UniqueId = formModel.UniqueId,
                Information = formModel.Item.Introduction ?? string.Empty,
                IsAjaxForm = formModel.Item.IsAjaxMvcForm,
                IsSaveFormDataToStorage = formModel.Item.IsSaveFormDataToStorage,
                Title = formModel.Item.FormName ?? string.Empty,
                Name = formModel.Item.FormName ?? string.Empty,
                TitleTag = formModel.Item.TitleTag.ToString(),
                ShowTitle = formModel.Item.ShowTitle,
                ShowFooter = formModel.Item.ShowFooter,
                ShowInformation = formModel.Item.ShowIntroduction,
                SubmitButtonName = formModel.Item.SubmitName ?? string.Empty,
                SubmitButtonPosition = formModel.Item.SubmitButtonPosition ?? string.Empty,
                SubmitButtonSize = formModel.Item.SubmitButtonSize ?? string.Empty,
                SubmitButtonType = formModel.Item.SubmitButtonType ?? string.Empty,
                SuccessMessage = formModel.Item.SuccessMessage ?? string.Empty,
                SuccessSubmit = false,
                Errors = formModel.Failures.Select(x => x.ErrorMessage).ToList(),
                Visible = true,
                LeftColumnStyle = formModel.Item.LeftColumnStyle,
                RightColumnStyle = formModel.Item.RightColumnStyle,
                Footer = formModel.Item.Footer,
                Item = formModel.Item.InnerItem,
                FormType = formModel.Item.FormType,
                ReadQueryString = formModel.ReadQueryString,
                QueryParameters = formModel.QueryParameters
            };

            // CSS Styles
            formViewModel.CssClass = string.Concat(formModel.Item.FormTypeClass ?? string.Empty, " ", formModel.Item.CustomCss ?? string.Empty, " ", formModel.Item.FormAlignment ?? string.Empty).Trim();

            ReflectionUtils.SetXmlProperties(formViewModel, formModel.Item.Parameters, true);

            formViewModel.Sections = formModel.Item.SectionItems.Select(x => this.GetSectionViewModel(new SectionItem(x), formViewModel)).Where(x => x != null).ToList();

            return formViewModel;
        }

        public void SetModelResults(FormViewModel view, IFormModel formModel)
        {
            Assert.ArgumentNotNull(view, "view");
            Assert.ArgumentNotNull(formModel, "formModel");

            var results = view.Sections.SelectMany(x => x.Fields).Select(x => ((IFieldResult)x).GetResult()).Where(x => x != null).ToList();

            foreach (var result in results)
            {
                if (result.Value == null) result.Value = string.Empty;
            }

            formModel.Results = results;
        }

        protected SectionViewModel GetSectionViewModel([NotNull] SectionItem item, FormViewModel formViewModel)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(formViewModel, "formViewModel");

            var sectionViewModel = new SectionViewModel
            {
                Fields = new List<FieldViewModel>(),
                Item = item.InnerItem
            };

            var title = item.Title;
            sectionViewModel.Visible = true;

            if (!string.IsNullOrEmpty(title))
            {
                sectionViewModel.ShowInformation = true;
                sectionViewModel.Title = item.Title ?? string.Empty;

                ReflectionUtils.SetXmlProperties(sectionViewModel, item.Parameters, true);

                sectionViewModel.ShowTitle = sectionViewModel.ShowLegend != "No";

                ReflectionUtils.SetXmlProperties(sectionViewModel, item.LocalizedParameters, true);
            }

            sectionViewModel.Fields = item.Fields.Select(x => this.GetFieldViewModel(x, formViewModel)).Where(x => x != null).ToList();

            if (!string.IsNullOrEmpty(item.Conditions))
            {
                RulesManager.RunRules(item.Conditions, sectionViewModel);
            }

            return !sectionViewModel.Visible ? null : sectionViewModel;
        }

        [CanBeNull]
        protected FieldViewModel GetFieldViewModel([NotNull] IFieldItem item, FormViewModel formViewModel)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(formViewModel, "formViewModel");

            var fieldType = item.MVCClass;
            if (string.IsNullOrEmpty(fieldType))
            {
                return new FieldViewModel
                {
                    Item = item.InnerItem
                };
            }

            var type = Type.GetType(fieldType);
            if (type == null)
            {
                return new FieldViewModel
                {
                    Item = item.InnerItem
                };
            }

            var fieldInstance = Activator.CreateInstance(type);
            var fieldViewModel = fieldInstance as FieldViewModel;

            if (fieldViewModel == null)
            {
                Log.Warn(string.Format("[WFFM]Unable to create instance of type {0}", fieldType), this);

                return null;
            }

            fieldViewModel.Title = item.Title ?? string.Empty;
            fieldViewModel.Name = item.Name ?? string.Empty;
            fieldViewModel.Visible = true;
            if (fieldViewModel is IHasIsRequired)
            {
                ((IHasIsRequired)fieldViewModel).IsRequired = item.IsRequired;
            }

            fieldViewModel.ShowTitle = true;
            fieldViewModel.Item = item.InnerItem;
            fieldViewModel.FormId = formViewModel.Item.ID.ToString();
            fieldViewModel.FormType = formViewModel.FormType;
            fieldViewModel.FieldItemId = item.ID.ToString();
            fieldViewModel.LeftColumnStyle = formViewModel.LeftColumnStyle;
            fieldViewModel.RightColumnStyle = formViewModel.RightColumnStyle;
            fieldViewModel.ShowInformation = true;

            var parameters = item.ParametersDictionary;
            parameters.AddRange(item.LocalizedParametersDictionary);
            fieldViewModel.Parameters = parameters;

            ReflectionUtil.SetXmlProperties(fieldInstance, item.ParametersDictionary);
            ReflectionUtil.SetXmlProperties(fieldInstance, item.LocalizedParametersDictionary);

            fieldViewModel.Parameters.AddRange(item.MvcValidationMessages);

            if (!fieldViewModel.Visible)
            {
                return null;
            }

            fieldViewModel.Initialize();

            if (!string.IsNullOrEmpty(item.Conditions))
            {
                RulesManager.RunRules(item.Conditions, fieldViewModel);
            }

            if (formViewModel.ReadQueryString)
            {
                if (formViewModel.QueryParameters != null && !string.IsNullOrEmpty(formViewModel.QueryParameters[fieldViewModel.Title]))
                {
                    var method = fieldViewModel.GetType().GetMethod("SetValueFromQuery");

                    if (method != null)
                    {
                        method.Invoke(fieldViewModel, new object[] { formViewModel.QueryParameters[fieldViewModel.Title] });
                    }
                }
            }

            return fieldViewModel;
        }
    }
}
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using PluralsightBot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs.Choices;
using PluralsightBot.Models;
using System.Text.RegularExpressions;
using PluralsightBot.Helpers;

namespace PluralsightBot.Dialogs
{
    public class BugReportDialog : ComponentDialog
    {
        #region Variables
        private readonly StateService _stateService;
        #endregion

        public BugReportDialog(string dialogId, StateService stateService) : base(dialogId)
        {
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));

            InitializeWaterfallDialog();
        }

        private void InitializeWaterfallDialog()
        {
            // Create Waterfall Steps
            var waterfallSteps = new WaterfallStep[]
            {
                DescriptionStepAsync,
                CallbackTimeStepAsync,
                PhoneNumberStepAsync,
                BugStepAsync,
                SummaryStepAsync
            };

            // Add Named Dialogs
            AddDialog(new WaterfallDialog($"{nameof(BugReportDialog)}.mainFlow", waterfallSteps));
            AddDialog(new TextPrompt($"{nameof(BugReportDialog)}.{PropertiesEnum.description}"));
            AddDialog(new DateTimePrompt($"{nameof(BugReportDialog)}.{PropertiesEnum.callbackTime}", CallbackTimeValidatorAsync));
            AddDialog(new TextPrompt($"{nameof(BugReportDialog)}.{PropertiesEnum.phoneNumber}", PhoneNumberValidatorAsync));
            AddDialog(new ChoicePrompt($"{nameof(BugReportDialog)}.{PropertiesEnum.bug}"));

            // Set the starting Dialog
            InitialDialogId = $"{nameof(BugReportDialog)}.mainFlow";
        }

        #region Waterfall Steps
        private async Task<DialogTurnResult> DescriptionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync($"{nameof(BugReportDialog)}.{PropertiesEnum.description}", new PromptOptions
            {
                Prompt = MessageFactory.Text("Enter a description for your report")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> CallbackTimeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values[PropertiesEnum.description.ToString()] = (string)stepContext.Result;

            return await stepContext.PromptAsync($"{nameof(BugReportDialog)}.{PropertiesEnum.callbackTime}", new PromptOptions
            {
                Prompt = MessageFactory.Text("Please enter in a callback time"),
                RetryPrompt = MessageFactory.Text("The value entered must be between the hours of 9 am and 5 pm."),
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> PhoneNumberStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values[PropertiesEnum.callbackTime.ToString()] = Convert.ToDateTime(((List<DateTimeResolution>)stepContext.Result).FirstOrDefault().Value);

            return await stepContext.PromptAsync($"{nameof(BugReportDialog)}.{PropertiesEnum.phoneNumber}",
            new PromptOptions
            {
                Prompt = MessageFactory.Text("Please enter in a phone number that we can call you back at"),
                RetryPrompt = MessageFactory.Text("Please enter a valid phone number"),
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> BugStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values[PropertiesEnum.phoneNumber.ToString()] = (string)stepContext.Result;

            return await stepContext.PromptAsync($"{nameof(BugReportDialog)}.{PropertiesEnum.bug}",
            new PromptOptions
            {
                Prompt = MessageFactory.Text("Please enter the type of bug."),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Security", "Crash", "Power", "Performance", "Usability", "Serious Bug", "Other" }),
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values[PropertiesEnum.bug.ToString()] = ((FoundChoice)stepContext.Result).Value;

            // Get the current profile object from user state.
            var userProfile = await _stateService.UserProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);

            // Save all of the data inside the user profile
            userProfile.Description = (string)stepContext.Values[PropertiesEnum.description.ToString()];
            userProfile.CallbackTime = (DateTime)stepContext.Values[PropertiesEnum.callbackTime.ToString()];
            userProfile.PhoneNumber = (string)stepContext.Values[PropertiesEnum.phoneNumber.ToString()];
            userProfile.Bug = (string)stepContext.Values[PropertiesEnum.bug.ToString()];

            // Show the summary to the user
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Here is a summary of your bug report:"), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(String.Format("Description: {0}", userProfile.Description)), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(String.Format("Callback Time: {0}", userProfile.CallbackTime)), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(String.Format("Phone Number: {0}", userProfile.PhoneNumber)), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(String.Format("Bug: {0}", userProfile.Bug)), cancellationToken);

            // Save data in userstate
            await _stateService.UserProfileAccessor.SetAsync(stepContext.Context, userProfile);

            // WaterfallStep alwaus finishes with the end of the Waterfall or with another dialog, here it is the end
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
        #endregion

        #region Validators
        private Task<bool> CallbackTimeValidatorAsync(PromptValidatorContext<IList<DateTimeResolution>> promptContext, CancellationToken cancellationToken)
        {
            var valid = false;

            if (promptContext.Recognized.Succeeded)
            {
                var resolution = promptContext.Recognized.Value.First();
                DateTime seletedDate = Convert.ToDateTime(resolution.Value);
                TimeSpan start = new(9, 0, 0); //9 o'clock
                TimeSpan end = new(17, 0, 0); // 5 o'clock

                if ((seletedDate.TimeOfDay >= start) && seletedDate.TimeOfDay <= end)
                {
                    valid = true;
                }

            }

            return Task.FromResult(valid);
        }

        private Task<bool> PhoneNumberValidatorAsync(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var valid = false;

            if (promptContext.Recognized.Succeeded)
            {
                var resolution = promptContext.Recognized.Value.First();
                valid = Regex.Match(promptContext.Recognized.Value, @"^(\+\d{1,2}\s)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}$").Success;
            }
            return Task.FromResult(valid);
        }

        #endregion
    }
}
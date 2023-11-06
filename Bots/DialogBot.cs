using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Logging;
using PluralsightBot.Services;
using Microsoft.Bot.Schema;
using System.Threading;
using PluralsightBot.Helpers;
using System.Collections.Generic;

namespace PluralsightBot.Bots
{
    public class DialogBot<T> : ActivityHandler where T : Dialog
    {
        #region Variables
        protected readonly Dialog _dialog;
        protected readonly StateService _stateService;
        protected readonly ILogger _logger;
        #endregion

        public DialogBot(StateService botStateService, T dialog, ILogger<DialogBot<T>> logger)
        {
            _stateService = botStateService ?? throw new ArgumentNullException(nameof(botStateService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occured during the turn
            await _stateService.UserState.SaveChangesAsync(turnContext);
            await _stateService.ConversationState.SaveChangesAsync(turnContext);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Running dialog with Message Activity.");

            // Run the dialog with the new message Activity
            await _dialog.Run(turnContext, _stateService.DialogStateAccessor,
                cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Say hi to begin!";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}

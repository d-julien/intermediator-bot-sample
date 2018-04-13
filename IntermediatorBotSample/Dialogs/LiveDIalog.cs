using IntermediatorBot.Strings;
using IntermediatorBotSample.CommandHandling;
using IntermediatorBotSample.MessageRouting;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using System.Threading.Tasks;
using Underscore.Bot.MessageRouting;

namespace IntermediatorBotSample.Dialogs
{
    /// <summary>
    /// Simple dialog that will only ever provide simple instructions.
    /// </summary>
    [Serializable]
    public class LiveDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext dialogContext)
        {
            dialogContext.Wait(OnMessageReceivedAsync);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Responds back to the sender with the simple instructions.
        /// </summary>
        /// <param name="dialogContext">The dialog context.</param>
        /// <param name="result">The result containing the message sent by the user.</param>
        private async Task OnMessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            IMessageActivity messageActivity = await result;
            string messageText = messageActivity.Text;
            var activity = (Activity)context.Activity;

            if (!string.IsNullOrEmpty(messageText))
            {
                MessageRouterResultHandler messageRouterResultHandler = WebApiConfig.MessageRouterResultHandler;
                await context.PostAsync($"Veuillez patienter, vous allez être mis en contact avec un humain.");
                messageActivity.Text = "human";
                var messageRouterResult = WebApiConfig.MessageRouterManager.RequestConnection((messageActivity as Activity));
                messageRouterResult.Activity = messageActivity as Activity;
                await messageRouterResultHandler.HandleResultAsync(messageRouterResult);
                context.Done(this);
            }
        }
    }
}

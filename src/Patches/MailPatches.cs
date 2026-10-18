using SPTushonka.Reflection.Il2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChatShared;
using EFT;
using EFT.UI;
using EFT.UI.Chat;
using HarmonyLib;
using SPTushonka.Reflection.Patching;

namespace UIFixes;

public static class MailPatches
{
    public static void Enable()
    {
        // Send read requests to the server
        new ActuallyReadMessagesPatch().SafeEnable();
        new ReadMessagesTimerPatch().SafeEnable();

        // Show the new message icons for messages with attachments
        new AttachmentsAreNewPatch().SafeEnable();
        new MenuAttachmentsAreNewPatch().SafeEnable();
        new DialogueViewAttachmentsAreNewPatch().SafeEnable();

        // Handle transfer items screen
        new UpdateCountsAfterTransferPatch().SafeEnable();

        // Block the places that clear AttachmentsNew
        new DialogueViewSelectedPatch().SafeEnable(); // Upon selecting a dialog
        new MenuTaskBarPatch().SafeEnable(); // Also upon selecting a dialog
        new SocialNetworkDisplayPatch().SafeEnable(); // Upon receiving a message via socket

        // Best effort to handle expired attachments
        new InvokeAttachmentsChangedPatch().SafeEnable();
        new HandleExpiredMessagesPatch().SafeEnable();
    }

    public class ActuallyReadMessagesPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SocialNetwork), nameof(SocialNetwork.UpdateReadMessages));
        }

        // Must be prefix because it fires events whose handlers will set Dialog.New to 0
        [PatchPrefix]
        public static void Prefix(SocialNetwork __instance)
        {
            if (__instance.SelectedDialogue != null)
            {
                __instance.AddDialogueToReadQueue(__instance.SelectedDialogue); // Adds it to list of dialogs to be marked read when the timer goes
            }
        }
    }

    public class ReadMessagesTimerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ChatScreen), nameof(ChatScreen.RepeatUpdateReadStatus));
        }

        [PatchPrefix]
        public static void Prefix(ChatScreen __instance)
        {
            // The target method is hardcoded to 80 seconds. Rather than try to change that, just lie to it about when it last sent
            var dateTime = __instance._lastResponse.AddSeconds(Settings.MailReadQueueTime.Value);
            if (DateTimeExtensions.UtcNow.Ticks > dateTime.Ticks)
            {
                __instance._lastResponse = new Il2CppSystem.DateTime(0L);
            }
        }
    }

    public class AttachmentsAreNewPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SocialNetwork), nameof(SocialNetwork.DisplayMessage), [typeof(DialogueChatMessage), typeof(UpdatableChatDialogue)]);
        }

        [PatchPrefix]
        public static void Prefix(DialogueChatMessage message, UpdatableChatDialogue dialogue)
        {
            // Normally it won't increment new if it has rewards
            if (message.HasRewards)
            {
                dialogue.New++;
            }
        }
    }

    public class MenuAttachmentsAreNewPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MenuTaskBar), nameof(MenuTaskBar.LastMessageUpdatedHandler));
        }

        [PatchPostfix]
        public static void Postfix(MenuTaskBar __instance, Il2CppSystem.Collections.Generic.KeyValuePair<UpdatableChatDialogue, DialogueChatMessage> pair)
        {
            if (!__instance._social.CanReadDialogue(pair.Key))
            {
                return;
            }

            // Normally it won't increment new if it has rewards
            if (pair.Value.HasRewards)
            {
                __instance.NewMessagesCount++;
            }
        }
    }

    public class DialogueViewAttachmentsAreNewPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(DialogueView), nameof(DialogueView.UpdateLastMessage));
        }

        [PatchPrefix]
        public static void Prefix(DialogueView __instance, DialogueChatMessage message)
        {
            if (__instance._selected)
            {
                return;
            }

            // Normally it won't increment new if it has rewards
            if (message.HasRewards)
            {
                __instance.NewMessagesCount = __instance._dialogue.New;
            }
        }
    }

    public class UpdateCountsAfterTransferPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(TransferItemsScreen), nameof(TransferItemsScreen.Close));
        }

        [PatchPostfix]
        public static async void Postfix(TransferItemsScreen __instance)
        {
            if (__instance._messages == null || !__instance._messages.Any())
            {
                return;
            }

            var dialogue = GetDialogFromMessage(__instance._messages.First(), __instance._itemUiContext.Session.SocialNetwork);
            if (dialogue == null)
            {
                return;
            }

            // Need to flush any moves
            await __instance._itemUiContext.ClientSession.FlushOperationQueue().AsManaged();

            // count messages that no longer have rewards
            var claimed = __instance._messages.Count(m => !m.DisplayRewardStatus);

            // Update dialog
            dialogue.AttachmentsNew -= claimed;
            if (dialogue.AttachmentsNew <= 0)
            {
                dialogue.HasMessagesWithRewards = false;
            }

            dialogue.OnDialogueAttachmentsChanged.Invoke();

            // If the chat screen is already open, I have to use UI to reach into it to update the attachment count
            var dialoguesContainer = MonoBehaviourSingleton<CommonUI>.Instance.ChatScreen.transform.Find("DialoguesPart").GetComponent<DialoguesContainer>();
            var dialogueViews = dialoguesContainer.GetComponentsInChildren<DialogueView>();
            var dialogueView = dialogueViews?.FirstOrDefault(dv => dv._dialogue == dialogue);
            if (dialogueView != null)
            {
                dialogueView.NewAttachmentsMessagesCount = dialogue.AttachmentsNew;
            }

            // update menu
            MonoBehaviourSingleton<PreloaderUI>.Instance.MenuTaskBar.SetNewMessagesCount();
        }

        private static UpdatableChatDialogue GetDialogFromMessage(DialogueChatMessage message, SocialNetwork socialNetwork)
        {
            string dialogueId;
            if (message.Member != null)
            {
                dialogueId = message.Member.Id;
            }
            else if (message.Type == EMessageType.SystemMessage)
            {
                dialogueId = socialNetwork.SystemMember.Id;
            }
            else
            {
                Plugin.Instance.Logger.LogError("UIFixes: Can't find dialogue ID from message");
                return null;
            }

            return socialNetwork.Dialogues.FirstOrDefault(d => d._id == dialogueId);
        }
    }

    // Block places that try to clear AttachmentsNew
    public class MenuTaskBarPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MenuTaskBar), nameof(MenuTaskBar.DialogueSelectedHandler));
        }

        [PatchPrefix]
        public static bool Prefix(MenuTaskBar __instance, UpdatableChatDialogue dialogue)
        {
            if (dialogue != null)
            {
                dialogue.New = 0;
            }

            __instance.SetNewMessagesCount();
            return false;
        }
    }

    public class DialogueViewSelectedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(DialogueView), nameof(DialogueView.SelectView));
        }

        // Transpilers can't edit il2cpp bodies; SelectView zeroes NewAttachmentsMessagesCount, so put it back afterwards.
        // The property is set-only, so read the current value back from the label it drives.
        [PatchPrefix]
        public static void Prefix(DialogueView __instance, ref int __state)
        {
            __state = 0;
            if (__instance._newAttachmentsMessagesObject != null && __instance._newAttachmentsMessagesObject.activeSelf)
            {
                int.TryParse(__instance._newAttachmentsMessagesCount?.text, out __state);
            }
        }

        [PatchPostfix]
        public static void Postfix(DialogueView __instance, int __state)
        {
            if (__state > 0)
            {
                __instance.NewAttachmentsMessagesCount = __state;
            }
        }
    }

    public class SocialNetworkDisplayPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SocialNetwork), nameof(SocialNetwork.DisplayMessage), [typeof(DialogueChatMessage), typeof(UpdatableChatDialogue)]);
        }

        [PatchPostfix]
        public static void Postfix(DialogueChatMessage message, UpdatableChatDialogue dialogue, int __state)
        {
            dialogue.AttachmentsNew = __state;

            if (message.HasRewards)
            {
                dialogue.HasMessagesWithRewards = true;
            }
        }

        // Transpilers can't edit il2cpp bodies; DisplayMessage zeroes AttachmentsNew, so put it back afterwards
        [PatchPrefix]
        public static void Prefix(UpdatableChatDialogue dialogue, ref int __state)
        {
            __state = dialogue.AttachmentsNew;
        }
    }

    // Normally this event is invoked when HasMessagesWithRewards changes - but it should be invoked on first load, because 
    // even if the value is the same, it's going from unloaded default value (false) to real value
    public class InvokeAttachmentsChangedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(UpdatableChatDialogue), nameof(UpdatableChatDialogue._UpdateDialogMessages_b__43_0));
        }

        [PatchPrefix]
        public static void Prefix(UpdatableChatDialogue __instance, ref bool __state)
        {
            __state = __instance.MessagesLoaded;
        }

        [PatchPostfix]
        public static void Postfix(UpdatableChatDialogue __instance, bool __state)
        {
            if (!__state && __instance.MessagesLoaded) // if this is first load and it was successful
            {
                __instance.OnDialogueAttachmentsChanged.Invoke();
            }
        }
    }

    public class HandleExpiredMessagesPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(DialogueView), nameof(DialogueView.Show));
        }

        // AttachmentsNew is only sent in intial load all dialogs request, and may included expired messages
        // HasMessagesWithRewards is sent when fetching specific dialog and is accurate due to server cleanup
        // Problem here is I only decrement AttachmentsNew when you transfer items
        // When you load a dialog it doesn't update AttachmentsNew, and with pagination I can't tell if there are more messages that have attachments
        // Can handle the 0 attachments case because HasMessagesWithRewards will be false, so in that case can set AttachmentsNew to 0
        // Leaves problematic case of HasMessagesWithRewards = true, but some have expired since the server cleaned up, so count will be off
        [PatchPostfix]
        public static void Postfix(DialogueView __instance)
        {
            __instance.R().UI.AddDisposable(__instance._dialogue.OnDialogueAttachmentsChanged.Bind((System.Action)(() =>
            {
                if (__instance._dialogue.MessagesLoaded && !__instance._dialogue.HasMessagesWithRewards && __instance._dialogue.AttachmentsNew > 0)
                {
                    __instance._dialogue.AttachmentsNew = 0;
                    __instance.NewAttachmentsMessagesCount = 0;
                    MonoBehaviourSingleton<PreloaderUI>.Instance.MenuTaskBar.SetNewMessagesCount();
                }
            })));
        }
    }
}
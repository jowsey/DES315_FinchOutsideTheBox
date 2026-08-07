using System;
using UI;
using UnityEngine;

namespace Util
{
    public class HintPromptWorldTrigger : MonoBehaviour
    {
        public enum HintPromptTriggerType
        {
            None,
            BalanceBeam,
            PressurePlate,
        }

        public HintPromptTriggerType Type = HintPromptTriggerType.None;

        private void OnTriggerEnter(Collider other)
        {
            switch (Type)
            {
                case HintPromptTriggerType.BalanceBeam:
                {
                    if (HintPrompt.HasShown.BalanceBeam) return;

                    HintPrompt.HasShown.BalanceBeam = true;
                    HintPrompt.RequestNew(new HintPrompt.HintPromptData
                    {
                        Title = "Watch out!",
                        Description = "The path ahead isn't always so straightforward!\n\nOvercome perilous obstacles and rough terrain with teamwork and co-operation!",
                    });

                    break;
                }
                case HintPromptTriggerType.PressurePlate:
                {
                    if (HintPrompt.HasShown.PressurePlate) return;

                    HintPrompt.HasShown.PressurePlate = true;
                    HintPrompt.RequestNew(new HintPrompt.HintPromptData
                    {
                        Title = "A strange contraption...",
                        Description = "Cats, caravans, and items alike can all be used to activate a pressure plate.\n\n" +
                                      "Doing so may change the environment around you, and reveal something new!",
                    });

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
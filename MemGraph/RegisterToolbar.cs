using UnityEngine;
using ToolbarControl_NS;

namespace MemGraph
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(FillItUp.MODID, FillItUp.MODNAME);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client
{
    public class ArchipelagoUI : MonoBehaviour
    {
        void OnGUI()
        {
            string ap_ver = "Archipelago v" + APState.AP_VERSION[0] + "." + APState.AP_VERSION[1] + "." + APState.AP_VERSION[2];

            if (APState.Session != null)
            {
                if (APState.Authenticated)
                {
                    GUI.Label(new Rect(16, 16, 300, 20), ap_ver + " Status: Connected");
                }
                else
                {
                    GUI.Label(new Rect(16, 16, 300, 20), ap_ver + " Status: Authentication failed");
                }
            }
            else
            {
                GUI.Label(new Rect(16, 16, 300, 20), ap_ver + " Status: Not Connected");
            }

            if ((APState.Session == null || !APState.Authenticated) && APState.State == APState.ConnectionState.Disconnected)
            {
                GUI.Label(new Rect(16, 36, 150, 20), "Host: ");
                GUI.Label(new Rect(16, 56, 150, 20), "PlayerName: ");
                GUI.Label(new Rect(16, 76, 150, 20), "Password: ");

                bool submit = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;

                APState.ConnectionInfo.host_name = GUI.TextField(new Rect(150 + 16 + 8, 36, 150, 20),
                    APState.ConnectionInfo.host_name);
                APState.ConnectionInfo.slot_name = GUI.TextField(new Rect(150 + 16 + 8, 56, 150, 20),
                    APState.ConnectionInfo.slot_name);
                APState.ConnectionInfo.password = GUI.TextField(new Rect(150 + 16 + 8, 76, 150, 20),
                    APState.ConnectionInfo.password);

                if (submit && Event.current.type == EventType.KeyDown)
                {
                    // The text fields have not consumed the event, which means they were not focused.
                    submit = false;
                }

                if ((GUI.Button(new Rect(16, 96, 100, 20), "Connect") || submit) && APState.ConnectionInfo.Valid)
                {
                    APState.Connect();
                }
            }
        }
    }
}

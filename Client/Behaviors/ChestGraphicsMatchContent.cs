using Archipelago.MonsterSanctuary.Client.AP;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client.Behaviors
{
    public class ChestGraphicsMatchContent : MonoBehaviour
    {
       // The numbers here are the sprite ID that's used by that color of chest
        public enum ChestColor
        {
            Brown = 0,
            Green = 33,
            Purple = 83
        }

        private Chest _chest;
        private tk2dSprite _sprite;
        private string _locationName;

        private ChestColor _color;
        public ChestColor Color => _color;

        public void Start()
        {
            _chest = gameObject.GetComponent<Chest>();
            _sprite = gameObject.GetComponent<tk2dSprite>();
            _locationName = $"{GameController.Instance.CurrentSceneName}_{_chest.ID}";

            UpdateSprite();
        }

        private void UpdateSprite()
        {
            if (!EnableChestGraphicsMatchContent())
                _color = ChestColor.Brown;

            else if (Locations.IsLocationProgression(_locationName))
                _color = ChestColor.Purple;

            else if (Locations.IsLocationUseful(_locationName))
                _color = ChestColor.Green;

            else
                _color = ChestColor.Brown;

            int spriteId = (int)_color;            

            // Set the base sprite of the chest
            if (_chest.CanInteract())
                _sprite.SetSprite(spriteId); // Closed sprite
            else
                _sprite.SetSprite(spriteId + 2); // Open sprite
        }

        private bool EnableChestGraphicsMatchContent()
        {
            return PlayerController.Instance.Inventory.Uniques.Any(i => i.Item.BaseName == "Looter's Handbook");
        }
    }
}

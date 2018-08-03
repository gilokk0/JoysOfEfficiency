﻿using StardewValley.Menus;
using System.Collections.Generic;

namespace JoysOfEfficiency.OptionsElements
{
    internal class MenuTab
    {
        public int Count { get { return _optionsElements.Count; } }

        private readonly List<OptionsElement> _optionsElements = new List<OptionsElement>();

        public void AddOptionsElement(OptionsElement element)
        {
            _optionsElements.Add(element);
        }

        public List<OptionsElement> GetElements()
        {
            return new List<OptionsElement>(_optionsElements);
        }
    }
}

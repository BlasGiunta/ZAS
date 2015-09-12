﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thought.vCards;
using VCFEditor.Model;
using System.ComponentModel;

namespace VCFEditor
{
    public interface IContactRepository
    {
        string fileName { get; set; }
        BindingList<Contact> Contacts { get; set; }
        
        BindingList<Contact> LoadContacts(string fileName);
        void SaveContacts(string fileName);
        vCard ParseContactAt(int index);
        void DeleteContact();
    }
}

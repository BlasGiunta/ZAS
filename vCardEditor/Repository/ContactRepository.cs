using System;
using System.IO;
using System.Linq;
using System.Text;
using Thought.vCards;
using VCFEditor.Model;
using System.ComponentModel;
using vCardEditor.Repository;
using System.Collections.Generic;

namespace VCFEditor.Repository
{
    public class ContactRepository : IContactRepository
    {
        public string fileName { get; set; }
        private IFileHandler _fileHandler;
        #region Contact Info
        /// <summary>
        /// Formatted name.
        /// </summary>
        public const string KeyName = "FN";

        /// <summary>
        /// Keep a copy of contact list when filtering
        /// </summary>
        private BindingList<Contact> OriginalContactList = null;
        /// <summary>
        /// Contact List
        /// </summary>
        private BindingList<Contact> _contacts;
        public BindingList<Contact> Contacts
        {
            get
            {
                if (_contacts == null)
                    _contacts = new BindingList<Contact>();
                return _contacts;
            }
            set
            {
                _contacts = value;
            }
        }
        #endregion

        public ContactRepository(IFileHandler fileHandler)
        {
            _fileHandler = fileHandler;
        }

        public BindingList<Contact> LoadContacts(string fileName)
        {
            this.fileName = fileName;

            StringBuilder RawContent = new StringBuilder();
            Contact contact = new Contact();
            string[] lines = _fileHandler.ReadAllLines(fileName);
            //TODO: Clean end of line from spaces..
            
            //Prevent from adding contacts to existings ones.
            Contacts.Clear();

            for (int i = 0; i < lines.Length; i++)
            {
                RawContent.AppendLine(lines[i]);
                if (lines[i].TrimEnd() == "END:VCARD")
                {
                    contact.card = ParseRawContent(RawContent);
                    Contacts.Add(contact);
                    contact = new Contact();
                    RawContent.Length = 0;
                }
              
            }

            OriginalContactList = Contacts;
            _dirty = false;
            return Contacts;
        }

        /// <summary>
        /// Save the contact to the file.
        /// </summary>
        /// <param name="path">Path to the new file, else if null, we overwrite the same file</param>
        public void SaveContacts(string fileName)
        {
            //overwrite the same file, else save as another file.
            if (string.IsNullOrEmpty(fileName))
                fileName = this.fileName;

            //Take a copy...
            if (!ConfigRepository.Instance.OverWrite)
            {
                string backupName = GetBackupName();
                _fileHandler.MoveFile(fileName, backupName);
            }

            StringBuilder sb = new StringBuilder();
            //Do not save the deleted ones...
            foreach (var entry in Contacts)
            {
                if (!entry.isDeleted)
                    sb.Append(generateRawContent(entry.card));
            }
                

            _fileHandler.WriteAllText(fileName, sb.ToString());

            _dirty = false;
        }

        private string GetBackupName()
        {
            int count = 0;
            string backupName = fileName + ".old" + count.ToString();

            while (_fileHandler.FileExist(backupName))
            {
                count++;
                backupName = fileName + ".old" + count.ToString();
            }

            return backupName;
        }


        /// <summary>
        /// Delete contacted that are selected.
        /// </summary>
        public void DeleteContact()
        {
            if (_contacts != null && _contacts.Count > 0)
            {
                //loop from the back to prevent index mangling...
                
                for (int i = _contacts.Count - 1; i > -1; i--)
                {
                    if (_contacts[i].isSelected)
                    {
                        _contacts[i].isDeleted = true;
                        _contacts.RemoveAt(i);
                        _dirty = true;
                    }
                        
                }
            }

        }


        /// <summary>
        /// Use the lib to parse a vcard chunk.
        /// </summary>
        /// <param name="rawContent"></param>
        /// <returns></returns>
        private vCard ParseRawContent(StringBuilder rawContent)
        {
            vCard card = null;

            using (MemoryStream s = GenerateStreamFromString(rawContent.ToString()))
            using (TextReader streamReader = new StreamReader(s, Encoding.UTF8))
            {
                card = new vCard(streamReader);
            }

            return card;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private MemoryStream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public BindingList<Contact> FilterContacts(string filter)
        {
            var list = OriginalContactList.Where(i => (i.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) && 
                                                    !i.isDeleted);
            Contacts = new BindingList<Contact>(list.ToList());
            return Contacts;
        }


        /// <summary>
        /// Save modified card info in the raw content.
        /// </summary>
        /// <param name="card"></param>
        /// <param name="index"></param>
        public void SaveDirtyFlag(int index)
        {
            if (index > -1)
                _contacts[index].isDirty = true;
        }

        public void SaveDirtyVCard(int index, vCard NewCard)
        {
            if (index > -1 && index < _contacts.Count-1 && _contacts[index].isDirty)
            {
                vCard card = _contacts[index].card;
                card.Title = NewCard.Title;
                card.FormattedName = NewCard.FormattedName;

                SavePhone(NewCard, card);
                SaveEmail(NewCard, card);
                SaveWebUrl(NewCard, card);

                _contacts[index].isDirty = false;
                _dirty = true;
            }
        }

        private void SavePhone(vCard NewCard, vCard card)
        {
            //HomePhone
            if (NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Home) != null)
            {
                if (card.Phones.GetFirstChoice(vCardPhoneTypes.Home) != null)
                    card.Phones.GetFirstChoice(vCardPhoneTypes.Home).FullNumber = NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Home).FullNumber;
                else
                    card.Phones.Add(new vCardPhone(NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Home).FullNumber, vCardPhoneTypes.Home));
            }
            else
            {
                if (card.Phones.GetFirstChoice(vCardPhoneTypes.Home) != null)
                    card.Phones.GetFirstChoice(vCardPhoneTypes.Home).FullNumber = string.Empty;

            }


            //Cellular
            if (NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Cellular) != null)
            {
                if (card.Phones.GetFirstChoice(vCardPhoneTypes.Cellular) != null)
                    card.Phones.GetFirstChoice(vCardPhoneTypes.Cellular).FullNumber = NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Cellular).FullNumber;
                else
                    card.Phones.Add(new vCardPhone(NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Cellular).FullNumber, vCardPhoneTypes.Cellular));
            }
            else
            {
                if (card.Phones.GetFirstChoice(vCardPhoneTypes.Cellular) != null)
                    card.Phones.GetFirstChoice(vCardPhoneTypes.Cellular).FullNumber = string.Empty;

            }

            //Work
            if (NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Work) != null)
            {
                if (card.Phones.GetFirstChoice(vCardPhoneTypes.Work) != null)
                    card.Phones.GetFirstChoice(vCardPhoneTypes.Work).FullNumber = NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Work).FullNumber;
                else
                    card.Phones.Add(new vCardPhone(NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Work).FullNumber, vCardPhoneTypes.Work));
            }
            else
            {
                if (card.Phones.GetFirstChoice(vCardPhoneTypes.Work) != null)
                    card.Phones.GetFirstChoice(vCardPhoneTypes.Work).FullNumber = string.Empty;

            }
        }

        private void SaveEmail(vCard NewCard, vCard card)
        {
            //Work
            if (NewCard.EmailAddresses.GetFirstChoice(vCardEmailAddressType.Internet) != null)
            {
                if (card.EmailAddresses.GetFirstChoice(vCardEmailAddressType.Internet) != null)
                    card.EmailAddresses.GetFirstChoice(vCardEmailAddressType.Internet).Address
                        = NewCard.EmailAddresses.GetFirstChoice(vCardEmailAddressType.Internet).Address;
                else
                    card.EmailAddresses.Add(new vCardEmailAddress(NewCard.Phones.GetFirstChoice(vCardPhoneTypes.Work).FullNumber,
                            vCardEmailAddressType.Internet));
            }
            else
            {
                if (card.EmailAddresses.GetFirstChoice(vCardEmailAddressType.Internet) != null)
                    card.EmailAddresses.GetFirstChoice(vCardEmailAddressType.Internet).Address = string.Empty;

            }

        }

        private void SaveWebUrl(vCard NewCard, vCard card)
        {
           
            if (NewCard.Websites.GetFirstChoice(vCardWebsiteTypes.Personal) != null)
            {
                if (card.Websites.GetFirstChoice(vCardWebsiteTypes.Personal) != null)
                    card.Websites.GetFirstChoice(vCardWebsiteTypes.Personal).Url = NewCard.Websites.GetFirstChoice(vCardWebsiteTypes.Personal).Url;
                else
                    card.Websites.Add(new vCardWebsite(NewCard.Websites.GetFirstChoice(vCardWebsiteTypes.Personal).Url, vCardWebsiteTypes.Personal));
            }
            else
            {
                if (card.Websites.GetFirstChoice(vCardWebsiteTypes.Personal) != null)
                    card.Websites.GetFirstChoice(vCardWebsiteTypes.Personal).Url = string.Empty;

            }


            if (NewCard.Websites.GetFirstChoice(vCardWebsiteTypes.Work) != null)
            {
                if (card.Websites.GetFirstChoice(vCardWebsiteTypes.Work) != null)
                    card.Websites.GetFirstChoice(vCardWebsiteTypes.Work).Url = NewCard.Websites.GetFirstChoice(vCardWebsiteTypes.Work).Url;
                else
                    card.Websites.Add(new vCardWebsite(NewCard.Websites.GetFirstChoice(vCardWebsiteTypes.Work).Url, vCardWebsiteTypes.Work));
            }
            else
            {
                if (card.Websites.GetFirstChoice(vCardWebsiteTypes.Work) != null)
                    card.Websites.GetFirstChoice(vCardWebsiteTypes.Work).Url = string.Empty;

            }
        }
        /// <summary>
        /// Generate a VCard class from a string.
        /// </summary>
        /// <param name="card"></param>
        /// <returns></returns>
        private string generateRawContent(vCard card)
        {
            vCardStandardWriter writer = new vCardStandardWriter();
            TextWriter tw = new StringWriter();
            writer.Write(card, tw);

            return tw.ToString();
        }

        /// <summary>
        /// Check if some iem in the contact list is modified
        /// </summary>
        /// <returns>true for dirty</returns>
        private bool _dirty;
        public bool dirty
        {
            get { return _dirty || (_contacts != null && _contacts.Any(x => x.isDirty)); }
            set { _dirty = value; }
        }

    }
}

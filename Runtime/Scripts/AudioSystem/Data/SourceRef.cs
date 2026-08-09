using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class SourceRef
    {
        public AudioSource Source {get; private set;}
        public string ID {get; private set;}
        public ExternalAudioSource Holder {get; private set;}

        private SourceRef(){}
        public SourceRef(AudioSource source, string ID, ExternalAudioSource holder)
        {
            this.Source = source;
            this.ID = ID;
            this.Holder = holder;
        }

        public bool CompareID(string otherID) => ID.Equals(otherID);
    }

}
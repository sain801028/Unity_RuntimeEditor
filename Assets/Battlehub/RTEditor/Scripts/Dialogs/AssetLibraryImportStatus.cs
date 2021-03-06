﻿using Battlehub.RTSaveLoad2.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Battlehub.RTEditor
{
    public class AssetLibraryImportStatus : MonoBehaviour
    {
        [SerializeField]
        private Sprite m_statusNone;

        [SerializeField]
        private Sprite m_statusNew;

        [SerializeField]
        private Sprite m_statusWarning;

        [SerializeField]
        private Sprite m_statusOverwrite;

        private Image m_image;

        private ImportStatus m_current;
        public ImportStatus Current
        {
            get { return m_current; }
            set
            {
                if(m_current != value)
                {
                    m_current = value;

                    switch(m_current)
                    {
                        case ImportStatus.None:
                            m_image.sprite = m_statusNone;
                            break;
                        case ImportStatus.New:
                            m_image.sprite = m_statusNew;
                            break;
                        case ImportStatus.Conflict:
                            m_image.sprite = m_statusWarning;
                            break;
                        case ImportStatus.Overwrite:
                            m_image.sprite = m_statusOverwrite;
                            break;
                    }
                }
            }
        }

        private void Awake()
        {
            m_image = GetComponent<Image>();
            m_image.sprite = m_statusNone;
        }

    }

}


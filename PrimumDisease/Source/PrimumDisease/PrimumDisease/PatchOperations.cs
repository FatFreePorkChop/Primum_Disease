using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using Verse;

namespace PrimumDisease
{

    /*
    * A custom patch operation to simplify sequence patch operations when defensively adding fields
    * Code by Lanilor (https://github.com/Lanilor)
    * This code is provided "as-is" without any warrenty whatsoever. Use it on your own risk.
    */
    public class PatchOperationAddOrReplace : PatchOperationPathed
    {

        protected string key;
        private XmlContainer value;

        protected override bool ApplyWorker(XmlDocument xml)
        {
            XmlNode valNode = value.node;
            bool result = false;
            IEnumerator enumerator = xml.SelectNodes(xpath).GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    object obj = enumerator.Current;
                    result = true;
                    XmlNode parentNode = obj as XmlNode;
                    XmlNode xmlNode = parentNode.SelectSingleNode(key);
                    if (xmlNode == null)
                    {
                        // Add - Add node if not existing
                        xmlNode = parentNode.OwnerDocument.CreateElement(key);
                        parentNode.AppendChild(xmlNode);
                    }
                    else
                    {
                        // Replace - Clear existing children
                        xmlNode.RemoveAll();
                    }
                    // (Re)add value
                    xmlNode.AppendChild(parentNode.OwnerDocument.ImportNode(valNode.FirstChild, true));
                }
            }
            finally
            {
                IDisposable disposable = enumerator as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
            return result;
        }
    }


    public class PatchOperationContext : Verse.PatchOperation
    {
        string xpath = string.Empty;

        List<Verse.PatchOperation> operations = null;

        [Unsaved]
        string lastFailedNode;
        [Unsaved]
        string lastFailedOperation;

        protected override bool ApplyWorker(XmlDocument xml)
        {
            var xmlPart = new XmlDocument();

            foreach (var xmlNode in xml.SelectNodes(xpath).Cast<XmlNode>().ToArray())
            {
                var node = xmlPart.ImportNode(xmlNode, true);
                xmlPart.AppendChild(node);

                foreach (Verse.PatchOperation operation in operations)
                {
                    if (!operation.Apply(xmlPart))
                    {
                        lastFailedNode = xmlNode.Name;
                        lastFailedOperation = operation.ToString();
                        return false;
                    }
                }

                var parentNode = xmlNode.ParentNode;

                parentNode.InsertBefore(parentNode.OwnerDocument.ImportNode(node, true), xmlNode);
                parentNode.RemoveChild(xmlNode);
            }

            return true;
        }

        public override void Complete(string modIdentifier)
        {
            base.Complete(modIdentifier);
            lastFailedNode = null;
            lastFailedOperation = null;
        }

        public override string ToString()
        {
            int num = ((operations != null) ? operations.Count : 0);
            string text = $"{base.ToString()}(count={num}";
            if (lastFailedOperation != null)
            {
                text = text + ", lastFailedNode=" + lastFailedNode + ", lastFailedOperation=" + lastFailedOperation;
            }
            return text + ")";
        }
    }
}
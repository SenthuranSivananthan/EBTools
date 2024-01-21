﻿using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForkPowerPointDeck
{
    public class SlideItem : MappingItem
    {
        public string IdentifyingText = "";
        public uint Id;
    };

    internal static class PresentationManagement
    {
        public static string[] GetAllTextInSlide(PresentationDocument presentationDocument, int slideIndex)
        {
            // Verify that the presentation document exists.
            if (presentationDocument == null)
            {
                throw new ArgumentNullException("presentationDocument");
            }

            // Verify that the slide index is not out of range.
            if (slideIndex < 0)
            {
                throw new ArgumentOutOfRangeException("slideIndex");
            }

            // Get the presentation part of the presentation document.
            PresentationPart presentationPart = presentationDocument.PresentationPart;

            // Verify that the presentation part and presentation exist.
            if (presentationPart != null && presentationPart.Presentation != null)
            {
                // Get the Presentation object from the presentation part.
                Presentation presentation = presentationPart.Presentation;

                // Verify that the slide ID list exists.
                if (presentation.SlideIdList != null)
                {
                    // Get the collection of slide IDs from the slide ID list.
                    var slideIds = presentation.SlideIdList.ChildElements;

                    // If the slide ID is in range...
                    if (slideIndex < slideIds.Count)
                    {
                        // Get the relationship ID of the slide.
                        string slidePartRelationshipId = (slideIds[slideIndex] as SlideId).RelationshipId;

                        // Get the specified slide part from the relationship ID.
                        SlidePart slidePart = (SlidePart)presentationPart.GetPartById(slidePartRelationshipId);

                        // Pass the slide part to the next method, and
                        // then return the array of strings that method
                        // returns to the previous method.
                        return GetAllTextInSlide(slidePart);
                    }
                }
            }
            // Else, return null.
            return null;
        }

        public static string[] GetAllTextInSlide(SlidePart slidePart)
        {
            // Verify that the slide part exists.
            if (slidePart == null)
            {
                throw new ArgumentNullException("slidePart");
            }

            // Create a new linked list of strings.
            LinkedList<string> texts = new LinkedList<string>();

            // If the slide exists...
            if (slidePart.Slide != null)
            {
                // Iterate through all the paragraphs in the slide.
                foreach (var paragraph in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>())
                {
                    // Create a new string builder.                    
                    StringBuilder paragraphText = new StringBuilder();

                    // Iterate through the lines of the paragraph.
                    foreach (var text in paragraph.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
                    {
                        // Append each line to the previous lines.
                        paragraphText.Append(text.Text);
                    }

                    if (paragraphText.Length > 0)
                    {
                        // Add each paragraph to the linked list.
                        texts.AddLast(paragraphText.ToString());
                    }
                }
            }

            if (texts.Count > 0)
            {
                // Return an array of strings.
                return texts.ToArray();
            }
            else
            {
                return null;
            }
        }

    }
}
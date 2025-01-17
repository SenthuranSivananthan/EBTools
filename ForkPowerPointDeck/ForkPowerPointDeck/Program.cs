﻿// See https://aka.ms/new-console-template for more information

//read in the base presentation
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using ForkPowerPointDeck;

string _baseFile = string.Empty;
string _outputFile = string.Empty;
string _slidesWithIdentifierToKeep = string.Empty;
bool _overwriteOutput = false;

//make sure we have all 3 necessary inputs: baseFile(b), outputFile(o), identifierToKeep(i)
//and then the optional one for overwrite(w)
#if DEBUG
Console.WriteLine("arg count = " + args.Count());
#endif
if (args.Count() < 3)
{
    Console.WriteLine("Missing required command line parameter!");
    Console.WriteLine("-b{baseFile without .pptx}");
    Console.WriteLine("-o{outputFile without .pptx}");
    Console.WriteLine("-i{identifier to keep}");
    Environment.Exit(-1);
}

//process the command line args
for (int i = 0; i < args.Length; i++)
{
    switch (args[i].Substring(1,1))
    {
        case "b":
            _baseFile = args[i].Substring(2) + ".pptx";

#if DEBUG
            Console.WriteLine("baseFile = " +  _baseFile);
#endif
            //if the input file doesn't exist, exit
            if (!File.Exists(_baseFile))
            {
                Console.WriteLine("Base file doesn't exist!");
                Environment.Exit(-1);
            }

            break;
        case "o":
            _outputFile = args[i].Substring(2) + ".pptx";
            break;
        case "i":
            _slidesWithIdentifierToKeep = args[i].Substring(2);

#if DEBUG
            Console.WriteLine("identifier to keep  = " + _slidesWithIdentifierToKeep);
#endif

            break;
        case "w":
            _overwriteOutput = true;
            break;
        default:
            // Code to handle unknown argument
            Console.WriteLine("Bad input parameter");
            Environment.Exit(-1);
            break;
    }
}

if (string.IsNullOrEmpty(_slidesWithIdentifierToKeep))
{
    Console.WriteLine("Slides to keep identifier not specified.");
    Environment.Exit(-1);
}

#if DEBUG
//tack a date/time on the end just to make iteratively testing easier
_outputFile = _outputFile.Replace(".pptx", DateTime.Now.ToString("yyyyMMddHHmmss") + ".pptx");

Console.WriteLine("outputFile = " + _outputFile);
#endif

//since we are overwriting any existing output file with the same name
//exit if the target output file already exists and w(overWrite) isn't set
if (!_overwriteOutput && File.Exists(_outputFile))
{
    Console.WriteLine("Output file already exists!");
    Environment.Exit(-1);
}

//make a copy of the base file
File.Copy(_baseFile, _outputFile, true);

//open up the doc
PresentationDocument presentationDocument = PresentationDocument.Open(_outputFile, true);

//get the presentation part from the presentation document.
PresentationPart? presentationPart = presentationDocument.PresentationPart;         
Presentation? presentation = presentationPart?.Presentation;

//get the slide list
SlideIdList? slideIdList = presentation.SlideIdList;

//make sure we actually have slides
if (slideIdList is null)
{
    throw new ArgumentNullException(nameof(slideIdList));
}

//unfortunately, no easy way ahead of time to know the slideId
//therefore, we'll just track which slides to keep/remove by mapping against the order in the deck
//this means we have to take a pass through and for each slide in order, compare the index against the mapping,
//then if it needs to be deleted, grab the SlideId and store it so we can loop through again later to delete
List<SlideItem> slidestoDelete=new List<SlideItem> ();
for (int slideIndex = 1; slideIndex < presentationPart.SlideParts.Count()+1; slideIndex++)
{
    // Get the slide
    SlideId? sourceSlide = slideIdList.ChildElements[slideIndex-1] as SlideId;

    string slideNotes = PresentationManagement.GetNotesInSlide(presentationDocument, slideIndex-1);

    //decide to keep or delete slide based on the input mapping file
    //if the slide needs removed, grab the SlideId and add it to the slidestoDelete arrary
    if (slideNotes.ToLowerInvariant().Contains(_slidesWithIdentifierToKeep.ToLowerInvariant()) == false)
    {
        Console.WriteLine($"Marking slide {slideIndex} with slide index {sourceSlide.Id} for removal.");
        SlideItem _slide = new SlideItem
        {
            Id = sourceSlide.Id,
            IdentifyingText = slideNotes
        };
        slidestoDelete.Add(_slide);
    }
    else
    {
        Console.WriteLine($"Keeping slide {slideIndex} with slide index {sourceSlide.Id}.");
    }

 }

//now that we have our list of slidestoDelete,
//iterate through the slide list and find them one by one
//when we find one, delete it, then restart the loop
//we have to restart the loop because the index has changed
foreach (var item in slidestoDelete)
{
    foreach  (SlideId slide in slideIdList.Elements<SlideId>())
    {
        if (slide.Id == item.Id)
        {
            Console.WriteLine($"Removing slide {slide.Id}.");
            slide.Remove();
            break;
        }
    }
}

//save the presentation to the doc
presentation.Save();

//save the doc
presentationDocument.Save();

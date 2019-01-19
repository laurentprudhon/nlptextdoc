# Language Resource Extractor

## Introduction

**Natural Language Processing** applications based on modern deep learning algorithms often rely on transfer learning techniques which require to pre-train a model on a large corpus of text.

To achieve good results, the corpus of text used in this pre-training step should be as close as possible to the specific inputs your NLP application will receive in production
- regional language
- business or knowledge domain
- document structure and sentence length
- grammar and spelling mistakes
- internal jargon and acronyms

While pre-trained networks for popular languages are often published jointly with the latest algorithms, they are almost always trained on generic Wikipedia or newspapers articles.

This approach is highly inefficient for several reasons
- the network has to learn a lot of knowledge domains not relevant for your application
- the network is trained to process well constructed sentences you will rarely encounter in real life
- entire families of specific business terms are never seen during pre-training
- the pre-processing steps chosen by the author may not be optimal for your specific task

To build an effective NLP application, you often need to **extract a business-domain specific corpus of text from real world documents** such as
- HTML pages from public web sites
- PDF or Word documents from entreprise repositories
- selected Wikipedia articles

The goals of this project are to provide
- a **standard format for text documents** used as input for NLP algorithms
- a **multiplatform tool to extract such text documents from popular sources** listed above 
- an **efficient process to collect text documents** related to your business domain
- a **text pre-processing library** based on the standard document format

The recommended approach is to
- First build a text corpus specific to your NLP application but independent of any algorithm
- Then use the pre-processing library to adapt this corpus to the format expected by a selected algorithm

## Standard Text Document Format

### Design principles

The current NLP algorithms don't know how to use the spatial position of a fragment text on a page, or the text decorations like colors, fonts, size, boldness, links, images ... They model a document as a stream of characters or words.

However, they rely on two important characteristics of the document structure
- The **relative order** of words and text blocks (along two axes in tables)
- A **hierarchical grouping** of text blocks (to compute higher level representations)

The goals of the Standard Text Document Format are to
- Preserve the relative order and hierarchical grouping of text blocks
- Remove every other text positioning and decoration information
- Keep full fidelity of the original character set, punctuation, line breaks
- Avoid any premature pre-processing or sentence segmentation operations
- Produce a human readable and easy to parse text document

### Key concepts

**NLPTextDocument** is a tree of *DocumentElement*s with three properties
- Title (string) : title of the document
- Uri (string) : universal resource identifier of the source document
- Timestamp (DateTime) : last modification date of the source document
- Metadata (Dictionary<string,string>) : optional key / value pairs used to describe the document

There are 4 families of *DocumentElement*s
   - **TextBlock** contains a single consistent block of text, a paragraph for example
   - **Section** contains a list of *DocumentElement*s, with an optional Title property
   - **List** and **NavigationList** contains a list of **ListItem**s, with an optional Title property
     - each ListItem is a list of *DocumentElement*s
   - **Table** contains a list of **TableHeader**s and **TableCell**s, with an optional Title property
     - each *TableElement* has Row/RowSpan and Col/ColSpan properties (position in a 2D array)
     - each *TableElement* is a list of *DocumentElement*s

Each DocumentElement has an integer **NestingLevel** property which tracks the depth of the element in the document tree.
Direct children of NLPTextDocument have a NestingLevel equal to 1.

### Standard file format

The recommended file extension is **.nlp.txt**.

The mandatory file encoding is **UTF-8**.

A standard file is read as a **stream of lines**
- lines are separated by a signle '\n' line feed character (10)
- line breaks are escaped by the two characters '\\' 'n' inside text blocks

Each line is parsed as
- a **DocumentElement delimiter or property** if it starts with the two characters '#' '#' 
- a TextBlock otherwise (add a space in front of a TextBlock starting with ## chars if needed)

The file starts with the three mandatory document properties, followed by optional metadata properties.
- \#\# NLPTextDocument Title ...value...
- \#\# NLPTextDocument Uri ...value...
- \#\# NLPTextDocument Timestamp ...value...
- \#\# NLPTextDocument Metadata [key]=...value...

Parsing rules
- the elements of these lines are separated by a single space character
- the property name must appear as is, uppercase and lowercase are important
- the property value starts after the space following the property name, until the end of line
- the metadata key can't contain the '=' character, the metadata value starts right after the '=' delimiter char
 
DocumentElement delimiters format : 
- \#\# [NestingLevel] [Section|List|NavigationList|Table] Start ...title...
- \#\# [NestingLevel] [ListItem] Start
- \#\# [NestingLevel] [TableHeader|TableCell] Start row,col
- \#\# [NestingLevel] [TableHeader|TableCell] Start row:rowspan,col:colspan
- \#\# [NestingLevel] [DocumentElementName] End <<...title...>>

Compact format for short lists :
- \#\# [NestingLevel] [List|NavigationList] Items [Title] >> [item 1] || [item 2] || [item 3]

Additional parsing rules
- nesting level is a positive integer value
- nesting level can be computed while reading the file by counting the nested Start / Stop delimiters
- but nesting level is mandatory in the file to help human readability
- document element name is one of : Section, List, ListItem, Table, TableHeader, TableCell
- Title property is optional and allowed only for Section, List and Table

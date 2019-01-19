from pathlib import Path
import re

class NLPTextDocumentReader:
    def __init__(self, rootpath):
        self.rootdir = Path(rootpath)
        
        self.DOCUMENT_ELEMENT_LINE_MARKER = "##"
        self.DOCUMENT_ELEMENT_START = "Start"
        self.DOCUMENT_ELEMENT_END = "End"
        self.DOCUMENT_ELEMENT_ITEMS = "Items"
        self.DOCUMENT_ELEMENT_ITEMS_START = ">>"
        self.DOCUMENT_ELEMENT_ITEMS_SEPARATOR = "||"
        
        self.TEXT_DOCUMENT_PROPERTY_PREFIX = self.DOCUMENT_ELEMENT_LINE_MARKER + " NLPTextDocument "
        self.TEXT_DOCUMENT_TITLE = "Title"        
        
        self.DOCUMENT_ELEMENT_LINE_REGEX = re.compile(
            self.DOCUMENT_ELEMENT_LINE_MARKER + " "
            + "(?P<NestingLevel>[0-9]+)" + " "
            + "(?P<ElementName>[A-Za-z]+)" + " "
            + "(?P<Command>" + self.DOCUMENT_ELEMENT_START + "|" + self.DOCUMENT_ELEMENT_END + "|" + self.DOCUMENT_ELEMENT_ITEMS + ")" + " ?")
        
    def __iter__(self):
        for textfile in self.rootdir.glob("**/*.nlp.txt"):
            with textfile.open(mode="r", encoding="utf-8-sig") as f:                 
                self.isreadingproperties = True
                for line in f:
                    line = line.strip()
                    if(not line): continue
                    for text in self.readline(line):
                        if(text):
                            yield text
                        else:
                            continue
                for text in self.onDocumentEnd() : yield text
                                        
    def readline(self,line):
        if (self.isreadingproperties):
            if (line.startswith(self.TEXT_DOCUMENT_PROPERTY_PREFIX)):
                for text in self.readproperty(line[len(self.TEXT_DOCUMENT_PROPERTY_PREFIX):]): yield text
            else:
                self.isreadingproperties = False
        if (not self.isreadingproperties):
            for text in self.readelement(line): yield text
                
    def readproperty(self,propstr):
        firstspaceindex = propstr.find(" ");
        if (firstspaceindex > 0):
            propertyname = propstr[:firstspaceindex]
            if(propertyname == self.TEXT_DOCUMENT_TITLE):
                title = propstr[firstspaceindex + 1:].strip()
                for text in self.onDocumentStart(title) : yield text
        yield None
    
    def readelement(self,line):
        if (line.startswith(self.DOCUMENT_ELEMENT_LINE_MARKER)):
            for text in self.readcommand(line): yield text
        else:
            yield line
    
    def readcommand(self,line):
        match = self.DOCUMENT_ELEMENT_LINE_REGEX.match(line)
        if(match): 
            nestingLevel = int(match.group("NestingLevel"))
            elementName = match.group("ElementName")
            command = match.group("Command")
            if (command == self.DOCUMENT_ELEMENT_START):
                title = line[match.end():].strip()
                if (len(title) == 0): title = None
                if(elementName == "Section"):
                    for text in self.onSectionStart(title): yield text
                elif(elementName == "NavigationList"):
                    for text in self.onNavigationListStart(title): yield text
                elif(elementName == "List"):
                    for text in self.onListStart(title): yield text
                elif(elementName == "ListItem"):
                    for text in self.onListItemStart(): yield text 
                elif(elementName == "Table"):
                    for text in self.onTableStart(title): yield text
                elif(elementName == "TableHeader"):
                    for text in self.onTableHeaderStart(): yield text                 
                elif(elementName == "TableCell"):
                    for text in self.onTableCellStart(): yield text
            elif (command == self.DOCUMENT_ELEMENT_END):
                if(elementName == "Section"):
                    for text in self.onSectionEnd(): yield text
                elif(elementName == "NavigationList"):
                    for text in self.onNavigationListEnd(): yield text
                elif(elementName == "List"):
                    for text in self.onListEnd(): yield text
                elif(elementName == "ListItem"):
                    for text in self.onListItemEnd(): yield text 
                elif(elementName == "Table"):
                    for text in self.onTableEnd(): yield text
                elif(elementName == "TableHeader"):
                    for text in self.onTableHeaderEnd(): yield text                 
                elif(elementName == "TableCell"):
                    for text in self.onTableCellEnd(): yield text 
            elif (command == self.DOCUMENT_ELEMENT_ITEMS):
                startOfItems = line.find(self.DOCUMENT_ELEMENT_ITEMS_START)
                title = line[match.end():startOfItems].strip()
                if (len(title) == 0): title = None
                if (elementName == "NavigationList"):
                    for text in self.onNavigationListStart(title): yield text
                elif (elementName == "List"):
                    for text in self.onListStart(title): yield text                 
                items = line[startOfItems+len(self.DOCUMENT_ELEMENT_ITEMS_START):].split(self.DOCUMENT_ELEMENT_ITEMS_SEPARATOR)
                for item in items:
                    item = item.strip()
                    if (len(item) >0):
                        for text in self.onInlineListItem(item): yield text
                if (elementName == "NavigationList"):
                    for text in self.onNavigationListEnd(): yield text
                elif (elementName == "List"):
                    for text in self.onListEnd(): yield text
            else:
                raise Exception(f"File format error on line : {line[:min(len(line), 50)]}");                     
        else:
            raise Exception(f"File format error on line {line[:min(len(line), 50)]}");
    
    def onDocumentStart(self,title):
        yield "<document-start>"
        if(title):
            yield "<document-title>" + " " + title
    
    def onDocumentEnd(self):
        yield "<document-end>"
    
    def onSectionStart(self,title):
        yield "<section-start>"
        if(title):
            yield "<section-title>" + " " + title
        
    def onSectionEnd(self): 
        yield "<section-end>"    
        
    def onNavigationListStart(self,title):
        yield "<navlist-start>"
        if(title):
            yield "<navlist-title>" + " " + title
        
    def onNavigationListEnd(self):
        yield "<navlist-end>"
        
    def onListStart(self,title):
        yield "<list-start>"
        if(title):
            yield "<list-title>" + " " + title
        
    def onListEnd(self):
        yield "<list-end>"
        
    def onInlineListItem(self,item):
        yield "<listitem>" + " " + item
        
    def onListItemStart(self):
        yield "<listitem-start>"
        
    def onListItemEnd(self):
        yield "<listitem-end>"
        
    def onTableStart(self,title):
        yield "<table-start>"
        if(title):
            yield "<table-title>" + " " + title
    
    def onTableEnd(self):
        yield "<table-end>"
        
    def onTableHeaderStart(self):
        yield "<tableheader-start>"
        
    def onTableHeaderEnd(self): 
        yield "<tableheader-end>"
        
    def onTableCellStart(self):
        yield "<tablecell-start>"
        
    def onTableCellEnd(self): 
        yield "<tablecell-end>"
                        
reader = NLPTextDocumentReader("C:\\Users\\user\\Desktop\\rootdir")
for line in reader:
    print(line)
import numpy as np
import pandas as pd
from pathlib import Path
import re

class NLPTextDocumentReader:
    def __init__(self, rootpath):
        self.rootpath = rootpath
        self.rootdir = Path(self.rootpath)
        
        self.documentCount = 0 
        self.nestingLevel = 1
        self.listType = []
        self.listCmd = []
        self.listLevel = []
        self.listText = []
                
        self.DOCUMENT_ELEMENT_LINE_MARKER = "##"
        self.DOCUMENT_ELEMENT_START = "Start"
        self.DOCUMENT_ELEMENT_END = "End"
        self.DOCUMENT_ELEMENT_ITEMS = "Items"
        self.DOCUMENT_ELEMENT_ITEMS_START = ">>"
        self.DOCUMENT_ELEMENT_ITEMS_SEPARATOR = "||"
        
        self.TEXT_DOCUMENT_PROPERTY_PREFIX = self.DOCUMENT_ELEMENT_LINE_MARKER + " NLPTextDocument "
        self.TEXT_DOCUMENT_TITLE = "Title"
        self.TEXT_DOCUMENT_URI = "Uri"
        
        self.DOCUMENT_ELEMENT_LINE_REGEX = re.compile(
            self.DOCUMENT_ELEMENT_LINE_MARKER + " "
            + "(?P<NestingLevel>[0-9]+)" + " "
            + "(?P<ElementName>[A-Za-z]+)" + " "
            + "(?P<Command>" + self.DOCUMENT_ELEMENT_START + "|" + self.DOCUMENT_ELEMENT_END + "|" + self.DOCUMENT_ELEMENT_ITEMS + ")" + " ?")
        
    def load_nlptextdocs(self):
        textdffile = self.rootdir / "nlptextdocs.dataframe.feather"
        if(textdffile.exists()):
            return pd.read_feather(textdffile)
        else:
            for textfile in self.rootdir.glob("**/*.nlp.txt"):
                with textfile.open(mode="r", encoding="utf-8-sig") as f:   
                    self.documentCount = self.documentCount+1
                    self.onDocumentStart(str(self.documentCount))
                    self.isreadingproperties = True
                    for line in f:
                        line = line.strip()
                        if(not line): continue
                        self.readline(line)
                    self.onDocumentEnd(str(self.documentCount))
            textdf = pd.DataFrame({"DocEltType": self.listType, "DocEltCmd" : self.listCmd, "NestingLevel": self.listLevel, "Text":self.listText})
            textdf = textdf.astype({"DocEltType": "category", "DocEltCmd": "category", "NestingLevel": np.uint8},copy=False)
            self.__init__(self.rootpath)
            textdf.to_feather(textdffile)
            return textdf

    def load_httplogs(self):
         return pd.read_csv(self.rootdir / "httprequests.log.csv", delimiter=";")

    def readline(self,line):
        if (self.isreadingproperties):
            if (line.startswith(self.TEXT_DOCUMENT_PROPERTY_PREFIX)):
                self.readproperty(line[len(self.TEXT_DOCUMENT_PROPERTY_PREFIX):])
            else:
                self.isreadingproperties = False
        if (not self.isreadingproperties):
            self.readelement(line)
                
    def readproperty(self,propstr):
        firstspaceindex = propstr.find(" ");
        if (firstspaceindex > 0):
            propertyname = propstr[:firstspaceindex]            
            propertyvalue = propstr[firstspaceindex + 1:].strip()
            if(propertyname == self.TEXT_DOCUMENT_TITLE):
                self.onDocumentTitle(propertyvalue)
            elif(propertyname == self.TEXT_DOCUMENT_URI):
                self.onDocumentUri(propertyvalue)       
    
    def readelement(self,line):
        if (line.startswith(self.DOCUMENT_ELEMENT_LINE_MARKER)):
            self.readcommand(line)
        else:
            self.onTextBlock(line)
    
    def readcommand(self,line):
        match = self.DOCUMENT_ELEMENT_LINE_REGEX.match(line)
        if(match): 
            self.nestingLevel = int(match.group("NestingLevel"))
            elementName = match.group("ElementName")
            command = match.group("Command")
            if (command == self.DOCUMENT_ELEMENT_START):
                title = line[match.end():].strip()
                if (len(title) == 0): title = None
                if(elementName == "Section"):
                    self.onSectionStart(title)
                elif(elementName == "NavigationList"):
                    self.onNavigationListStart(title)
                elif(elementName == "List"):
                    self.onListStart(title)
                elif(elementName == "ListItem"):
                    self.onListItemStart()
                elif(elementName == "Table"):
                    self.onTableStart(title)
                elif(elementName == "TableHeader"):
                    self.onTableHeaderStart()           
                elif(elementName == "TableCell"):
                    self.onTableCellStart()
            elif (command == self.DOCUMENT_ELEMENT_END):
                if(elementName == "Section"):
                    self.onSectionEnd()
                elif(elementName == "NavigationList"):
                    self.onNavigationListEnd()
                elif(elementName == "List"):
                    self.onListEnd()
                elif(elementName == "ListItem"):
                    self.onListItemEnd()
                elif(elementName == "Table"):
                    self.onTableEnd()
                elif(elementName == "TableHeader"):
                    self.onTableHeaderEnd()                 
                elif(elementName == "TableCell"):
                    self.onTableCellEnd()
            elif (command == self.DOCUMENT_ELEMENT_ITEMS):
                startOfItems = line.find(self.DOCUMENT_ELEMENT_ITEMS_START)
                title = line[match.end():startOfItems].strip()
                if (len(title) == 0): title = None
                if (elementName == "NavigationList"):
                    self.onNavigationListStart(title)
                elif (elementName == "List"):
                    self.onListStart(title)             
                self.nestingLevel = self.nestingLevel+1
                items = line[startOfItems+len(self.DOCUMENT_ELEMENT_ITEMS_START):].split(self.DOCUMENT_ELEMENT_ITEMS_SEPARATOR)
                for item in items:
                    item = item.strip()
                    if (len(item) > 0):
                        self.onInlineListItem(item)
                self.nestingLevel = self.nestingLevel-1
                if (elementName == "NavigationList"):
                    self.onNavigationListEnd()
                elif (elementName == "List"):
                    self.onListEnd()
            else:
                raise Exception(f"File format error on line : {line[:min(len(line), 50)]}");                     
        else:
            raise Exception(f"File format error on line {line[:min(len(line), 50)]}");
    
    def onDocumentStart(self,docId):
        self.appendrow("Document","Start",docId)
    
    def onDocumentTitle(self,title):
        self.appendrow("Document","Title",title)
            
    def onDocumentUri(self,uri):
        self.appendrow("Document","Uri",uri)
    
    def onDocumentEnd(self,docId):
        self.appendrow("Document","End",docId)
    
    def onTextBlock(self,text):
        self.appendrow("TextBlock","Text",text)
            
    def onSectionStart(self,title):
        self.appendrow("Section","Start",title)
        
    def onSectionEnd(self): 
        self.appendrow("Section","End")
        
    def onNavigationListStart(self,title):
        self.appendrow("NavigationList","Start",title)
        
    def onNavigationListEnd(self):
        self.appendrow("NavigationList","End")
        
    def onListStart(self,title):
        self.appendrow("List","Start",title)
        
    def onListEnd(self):
        self.appendrow("List","End")
        
    def onInlineListItem(self,item):
        self.appendrow("ListItem","Text",item)
            
    def onListItemStart(self):
        self.appendrow("ListItem","Start")
        
    def onListItemEnd(self):
        self.appendrow("ListItem","End")
        
    def onTableStart(self,title):
        self.appendrow("Table","Start",title)
    
    def onTableEnd(self):
        self.appendrow("Table","End")
        
    def onTableHeaderStart(self):
        self.appendrow("TableHeader","Start")
        
    def onTableHeaderEnd(self): 
        self.appendrow("TableHeader","End")
        
    def onTableCellStart(self):
        self.appendrow("TableCell","Start")
        
    def onTableCellEnd(self): 
        self.appendrow("TableCell","End")
            
    def appendrow(self,docEltType,docEltCmd,text=None):
            self.listType.append(docEltType)
            self.listCmd.append(docEltCmd)
            self.listLevel.append(self.nestingLevel)
            self.listText.append(text)
                        
textreader = NLPTextDocumentReader(websitedir)
textdf = textreader.load_nlptextdocs()
logsdf = textreader.load_httplogs()

print(f"{websitedir} : {len(textdf)} texts {logsdf} logs")
print(logsdf["Status code"].value_counts())
from pathlib import Path

class NLPTextDocumentReader:
    def __init__(self, rootpath):
        self.rootdir = Path(rootpath)
        
        self.DOCUMENT_ELEMENT_LINE_MARKER = "##"
        self.TEXT_DOCUMENT_PROPERTY_PREFIX = self.DOCUMENT_ELEMENT_LINE_MARKER + " NLPTextDocument ";
        self.TEXT_DOCUMENT_TITLE = "Title"
        
    def __iter__(self):
        for textfile in rootdir.glob("**/*.nlp.txt"):
            with textfile.open(mode="r", encoding="utf-8-sig") as f: 
                yield self.ondocumentstart()
                self.isreadingproperties = True
                for line in f:
                    line = line.strip()
                    if(not line): continue
                    text = self.parseline(line)
                    if(text):
                        yield text
                    else:
                        continue
                                        
    def parseline(self,line):
        if (self.isreadingproperties):
            if (line.startswith(self.TEXT_DOCUMENT_PROPERTY_PREFIX)):
                return self.readproperty(line[len(self.TEXT_DOCUMENT_PROPERTY_PREFIX):])
            else:
                self.isreadingproperties = False
        if (not self.isreadingproperties):
            return self.readelement(line)
                
    def readproperty(self,propstr):
        firstspaceindex = propstr.find(" ");
        if (firstspaceindex > 0):
            propertyname = propstr[:firstspaceindex]
            if(propertyname == self.TEXT_DOCUMENT_TITLE):
                return "<nlptextdoc-title> " + propstr[firstspaceindex + 1:].strip();
        return None
    
    def readelement(self,line):
        return "ELEM - " + line
    
    def ondocumentstart(self):
        return "<nlptextdocument>"
        
        
                
reader = NLPTextDocumentReader("C:\\Users\\user\\Desktop\\rootdir")
for line in reader:
    print(line)

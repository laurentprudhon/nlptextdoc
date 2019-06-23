""" NLP resources extractor - Python lib

HOW TO IMPORT THIS MODULE in a notebook
> %reload_ext autoreload
> %autoreload 2
> import sys
> sys.path.append("{PATH_TO_THIS_FILE}/nlptextdoc.python")

INSTALL pandas with pyarrow.feather file format support
1. conda install pandas
2. conda install pyarrow
3. Make sure your installed version of pandas is > 0.24
> pd.show_versions()

INSTALL spaCy with french language support
1. conda install -c conda-forge spacy ()
2. python -m spacy download fr
3. Make sure your installed version of spacy is > 2.1 
python -m spacy info

HOW TO RUN A JUPYTER NOTEBOOK in the context of a conda environment
1. conda activate myenvname
2. conda install ipykernel
3. python -m ipykernel install --user --name myenvname --display-name "Python (myenvname)"
4. jupyter notebook
5. => menu Kernel / Change kernel
6. Check : locate the Jupyter config directories, kernels are configured in the 'kernels' subdirectory, in 'kernel.json' files
> from jupyter_core.paths import jupyter_data_dir
> print(jupyter_data_dir())
7. Check : locate the python environment in use
> import sys
> print(sys.executable)

See : https://github.com/laurentprudhon/nlptextdoc
"""

import os
import sys
from pathlib import Path
from hashlib import md5
from collections import defaultdict
import re
from urllib.request import urlopen
from urllib.error import HTTPError

import numpy as np
import pandas as pd

import spacy
nlp = spacy.load("fr_core_news_sm")
from spacy.tokenizer import Tokenizer
tokenizer = Tokenizer(nlp.vocab)

class NLPTextDocumentReader:
    """Read output files of a website extraction in pandas DataFrames.
    
    Sample usage :
    
    textreader = NLPTextDocumentReader(websitedir)
    textdf = textreader.load_nlptextdocs()
    logsdf = textreader.load_httplogs()
    
    print(f"{websitedir} : {len(textdf)} texts {logsdf} logs")
    print(logsdf["Status code"].value_counts())
    """    
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
        
    def load_dataframe(self):
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
		if (text != None):
			text = text.replace("\\n","\n")
		self.listText.append(text)
  
def prepareDataFramesForWebsites(rootdir, websites):
	"""Loads all individual text blocks extracted from the pages of each website in a dataframe, and save them efficiently on disk.

	Parameters:
	rootdir - Path to the directory where the websites were extracted
	websites - List of strings with the websites root URLs
	"""
	for website in websites:
		websitedir = rootdir / website
		print(f"Preparing dataframe for website {website} ...")
		reader = NLPTextDocumentReader(websitedir)
		textdf = reader.load_dataframe()
		docsCount = len(textdf[(textdf["DocEltType"]=="Document") & (textdf["DocEltCmd"]=="Start")])
		logsdf = loadExtractionLogs(websitedir)
		print(f"- {len(logsdf)} website extraction logs")
		print(logsdf["Status code"].value_counts())
		print(f"- {docsCount)} documents")
		print(f"- {len(textdf)} document elements")
		print(f"- dataframe size in memory : {_format_size_mb(_memory_size(textdf))} MB")
		websitefile = websitedir / "nlptextdocs.dataframe.feather"
		print(f"- dataframe size on disk : {_format_size_mb(_file_size(websitefile))} MB")


def createDatasetFromWebsites(rootdir, websites, minWordsCount=5):
	"""Combine all textblocks from each website in a single dataframe, while applying several filters to enhance the dataset quality:
	- keep only distinct text blocks for each website
	- keep only text blocks with more than 5 words
	- keep only text blocks in french

	Create at the same time 4 additional dataframes:
	- a dictionary of all disctinct words encountered in the dataset by decreasing frequency
	- a dictionary of all disctinct characters encountered in the dataset by decreasing frequency
	- a table of the dataset statistics

	Parameters:
	rootdir - Path to the directory where the websites were extracted
	websites - List of strings with the websites root URLs
	"""
	charsCount = 0	
	wordsCount = 0
	vocabdict = defaultdict(lambda:0)
	listSiteIndex = []
	listRowIndex = []
	listText = []
	for idx,website in enumerate(websites):
		websitedir = rootdir / website
		hashes = set()
		print(f"Loading dataframe for website {website} ...")
		reader = NLPTextDocumentReader(websitedir)
		textdf = reader.load_nlptextdocs()
		print(f"- filtering and tokenizing {len(textdf)} text blocks ...")
		websitetexts = textdf[((textdf["DocEltType"] != "Document") | (textdf["DocEltCmd"] == "Title")) & (textdf["DocEltCmd"] != "End") & ~textdf["Text"].isnull()]["Text"]
		localWordsCount = 0
		for rowidx,text in websitetexts.iteritems():
			hval = md5(text.encode()).digest()
			if not (hval in hashes):         
				hashes.add(hval)
				doc = tokenizer(text)
				if len(doc) >= minWordsCount:
					charsCount = charsCount + len(text)
					localWordsCount = localWordsCount + len(doc)
					for token in doc:
						vocabdict[token.text] = vocabdict[token.text] + 1
					listSiteIndex.append(idx)
					listRowIndex.append(rowidx)
					listText.append(text)
		print(f"- this website contributed {localWordsCount} words to the dataset")
		wordsCount = wordsCount + localWordsCount

	print("Saving the complete dataset ...")
	datasetdf = pd.DataFrame({"SiteIndex": listSiteIndex, "RowIndex" : listRowIndex, "Text":listText})
	print(f"- {charsCount} characters, {wordsCount} words, {len(datasetdf)} text blocks")
	print(f"- dataset size in memory : {_format_size_mb(_memory_size(datasetdf))} MB")
	datasetfile = rootdir / "dataset.dataframe.feather"
	datasetdf.to_feather(datasetfile)
	print(f"- dataset size on disk : {_format_size_mb(_file_size(datasetfile))} MB")
	
	vocabdf = saveVocabulary(rootdir, vocabdict)	
	charsetdf = saveCharset(rootdir, vocabdf)

def saveVocabulary(rootdir, vocabdict):
	print("Saving the vocabulary ...")
	vocabdf = pd.DataFrame({"Word" : [*vocabdict.keys()], "Count" : [*vocabdict.values()]})	
	vocabdf.sort_values("Count", ascending=False, inplace=True)
	vocabdf.reset_index(inplace=True)
	lexicontags = _buildLexiconTags(rootdir)	
	vocabdf["LexiconTags"] = vocabdf["Word"].apply(lambda word: _getTokenTags(str(word),lexicontags))
	dictionarytags _buildDictionaryTags(rootdir)
	vocabdf["DictionaryTags"] = vocabdf["Word"].apply(lambda word: getannot(str(word),dictionarytags))
	vocabdf["CommonTags"] = wordsdf.apply(lambda row: _mergeTokenTags(str(row["LexiconTags"]),str(row["DictionaryTags"])),axis=1)
	vocabfile = rootdir / "vocabulary.dataframe.feather"
	vocabdf.to_feather(vocabfile)
	vocabdf.to_csv(rootdir / "charset.csv",sep=";")
	print(f"- {len(vocab)} distinct words")
	return vocabdf

def _buildLexiconTags(rootdir):
	lexicondf = pd.read_csv(rootdir / "data" / "UDLex_French-Lefff1.conllul.txt",sep="\t")
	lexicontags = {}
	for index, row in lexicondf.iterrows():
		token = row["!"]
		tag = row["PUNCT"]
		if(not (token in lexicontags)):
			lexicontags[token] = tag
		elif(not (tag in lexicontags[token])):
			lexicontags[token] = lexicontags[token] + "|" + tag
	return lexicontags

def _buildDictionaryTags(rootdir):
	dictionarydf = pd.read_csv(rootdir / "data" / "lexique-dicollecte-fr-v6.4.1.tsv",sep="\t")
	dictionarytags = {}
	for index, row in dictionarydf.iterrows():
		token = row["Flexion"]
		tag = _convertDicollecteTagsToUnivDepTags(row["Étiquettes"])
		if(not (token in dictionarytags)):
			dictionarytags[token] = tag
		elif(not (tag in dictionarytags[token])):
			dictionarytags[token] = dictionarytags[token] + "|" + tag
	return dictionarytags

def _convertDicollecteTagsToUnivDepTags(text):
    if(("adj" in text) or ("loc.adj" in text)):
        return "ADJ"
    elif("prep" in text):
        return "ADP"
    elif(("adv" in text) or ("loc.adv" in text)):
        return "ADV"
    elif(("v0a" in text) or ("v0e" in text) or ("ppas" in text)):
        return "AUX"
    elif("cjco" in text):
        return "CCONJ"
    elif("det" in text):
        return "DET"
    elif("interj" in text):
        return "INTJ"
    elif("nom" in text):
        return "NOUN"
    elif(("nb" in text) or ("ord" in text)):
        return "NUM"
    elif("pro" in text):
        return "PRON"
    elif(("prn" in text) or ("patr" in text) or ("npr" in text)):
        return "PROPN"
    elif("cjsub" in text):
        return "SCONJ"
    elif("symb" in text):
        return "SYM"
    elif(("v1" in text) or ("v2" in text) or ("v3" in text) or ("loc.verb" in text)):
        return "VERB"
    else:
        return text

def _getTokenTags(token,tags):
    annot = tags.get(token)
    if(annot is None):
        annot = tags.get(token.lower())
    return annot

def _mergeTokenTags(annot1,annot2):
    if(annot1 == annot2):
        return annot1
    elif((annot1 != "None") and (annot2 == "None")):
        return annot1
    elif((annot1 == "None") and (annot2 != "None")):
        return annot2
    elif(len(annot1) == len(annot2)):
        if("|" in annot1):
            tags1 = sorted(annot1.split("|"))
            tags2 = sorted(annot2.split("|"))
            for tag in tags1:
                if(not (tag in tags2)):
                    return 0
            return annot1
        else:
            return 0
    else:
        candidate_value = annot1
        if(len(annot1) > len(annot2)):
            temp = annot1
            annot1 = annot2
            annot2 = temp
            candidate_value = annot2
        tags1 = annot1.split("|")
        for tag in tags1:
            if(not (tag in annot2)):
                return 0
        return candidate_value

def saveCharset(rootdir, vocabdf):
	print("Saving the character set ...")
	charset = defaultdict(lambda:0)
	for idx,row in vocabdf.iterrows():
		token = row["Words"]
		count = row["Counts"]
		for char in token:
			charcode = ord(char)
			charcounts[charcode] = charcounts[charcode] + count
	charsetdf = pd.DataFrame({"Code" : [*charset.keys()], "Count" : [*charset.values()]})	
	charsetdf.sort_values("Count", ascending=False, inplace=True)
	charsetdf.reset_index(inplace=True)
	charsetdf["Char"] = charsetdf.index.map(lambda x:chr(x))
	charsetdf["isAlpha"] = charsetdf["Char"].map(lambda x:x.isalpha())
	charsetdf["isDigit"] = charsetdf["Char"].map(lambda x:x.isdigit())
	charsetdf["isSpace"] = charsetdf["Char"].map(lambda x:x.isspace())
	charsetdf["Percent"] = 100*charsetdf["Count"].cumsum()/charsetdf["Count"].sum()	
	charsetfile = rootdir / "charset.dataframe.feather"
	charsetdf.to_feather(charsetfile)
	charsetdf.to_csv(rootdir / "charset.csv",sep=";")
	print(f"- {len(charset} distinct characters")
	return charsetdf

def loadDataset(rootdir):
	datasetfile = rootdir / "dataset.dataframe.feather"
	return pd.read_feather(datasetfile)

def loadVocabulary(rootdir):
	vocabfile = rootdir / "vocabulary.dataframe.feather"
	return pd.read_feather(vocabfile)

def loadCharset(rootdir):
	charsetfile = rootdir / "charset.dataframe.feather"
	return pd.read_feather(charsetfile)

def listSeparatorChars(charsetdf):
	return charsetdf[(charsetdf["isAlpha"] == False) & (charsetdf["isDigit"] == False)]

def getTextBlocksWithSeparatorChars(datasetdf, charcode, samplecount):
	sampledf = datasetdf.loc[datasetdf["Text"].str.contains(chr(charcode),regex=False)]
	sampledf = sampledf.sample(min(samplecount,len(sampledf)))
	sampledf.to_csv(rootdir / f"char_{charcode}.csv",sep=";")

def listWebsiteDirs(rootdir):
	return [websitedir for websitedir in rootdir.iterdir() if websitedir.is_dir()]

def loadExtractionLogs(websitedir):
	return pd.read_csv(websitedir / "httprequests.log.csv",delimiter=";")

def checkExtractionLogsByErrorType(logsdf, errorTypeIndex):
	errorTypes = ["NotFound","Redirect","NoContent","Forbidden","BadRequest","Moved"]
	errorType = errorTypes[errorTypeIndex]
	urlsWithError = logsdf[logsdf["Status code"] == errorType]["Url"]
	print(f"Testing {len(urlsWithError)} URLs with error type {errorType} ...")
	errorcodes = []
	for url in urlsWithError:
		try:
			resp = urlopen(url)
			errorcodes.append(resp.getcode())
		except HTTPError as he:
			errorcodes.append(he.code)
	checksdf = pd.DataFrame({"Urls" : urlsWithError, "StatusCodes" : errorcodes})	
	print(checksdf["StatusCodes"].value_counts())
	return checksdf
		
def listFrenchFinanceWebsites():
	return ["http://bourse.latribune.fr/",
			"http://cercledelepargne.com/",
			"http://finance.lelynx.fr/banques/",
			"http://labourseauquotidien.fr/",
			"http://lafourmiz.fr/",
			"http://www.assurances.com/",
			"http://www.banque.org/",
			"http://www.banque-info.com/",
			"http://www.bourse.fr/",
			"http://www.boursedirect.fr/",
			"http://www.capitaine-epargne.com/",
			"http://www.cnp.fr/",
			"http://www.cofinoga.fr/",
			"http://www.comparabanques.fr/",
			"http://www.comparalivrets.fr/",
			"http://www.fbf.fr/",
			"http://www.financo.fr/",
			"http://www.generali.fr/",
			"http://www.guide-epargne.com/",
			"http://www.lemonde.fr/epargne/",
			"http://www.leparisien.fr/actus/banque",
			"http://www.lesaffaires.com/bourse",
			"http://www.lesclesdelabanque.com",
			"http://www.msn.com/fr-fr/finance",
			"http://www.retraiteepargne.fr/",
			"http://www.revue-banque.fr/",
			"http://www.strategie-bourse.com/",
			"http://www.zonebourse.com/",
			"https://acpr.banque-france.fr/",
			"https://banque.meilleurtaux.com/",
			"https://bourse.lefigaro.fr/",
			"https://compte-nickel.fr/",
			"https://eko-by-ca.fr/",
			"https://epargne.ooreka.fr/",
			"https://ffa-assurance.fr/",
			"https://fr.finance.yahoo.com/",
			"https://humanis.com/",
			"https://mabanque.bnpparibas/",
			"https://mes-placements.fr/",
			"https://n26.com/fr-fr/",
			"https://particulier.apicil.com/",
			"https://www.10meilleuresbanques.fr/",
			"https://www.abcbourse.com/",
			"https://www.afer.fr/",
			"https://www.ag2rlamondiale.fr/",
			"https://www.agpm.fr/",
			"https://www.allianz.fr/",
			"https://www.allianzbanque.fr/",
			"https://www.amaguiz.com/",
			"https://www.ameli.fr/",
			"https://www.amundi.fr/fr_part",
			"https://www.arkea.com/",
			"https://www.assurland.com/",
			"https://www.aviva.fr/",
			"https://www.axa.fr/",
			"https://www.banque.fr/",
			"https://www.banque-casino.fr/",
			"https://www.banque-edel.fr/",
			"https://www.banque-france.fr/",
			"https://www.banquepopulaire.fr/",
			"https://www.banquesenligne.org/",
			"https://www.bforbank.com/",
			"https://www.boursedeparis.fr/",
			"https://www.boursier.com/",
			"https://www.boursorama.com/",
			"https://www.boursorama-banque.com/",
			"https://www.bred.fr/",
			"https://www.ca-alsace-vosges.fr/",
			"https://www.caisse-epargne.fr/",
			"https://www.carrefour-banque.fr/",
			"https://www.cbanque.com/",
			"https://www.cetelem.fr/",
			"https://www.challenges.fr/tag_theme/banque_876/",
			"https://www.cic.fr/",
			"https://www.cofidis.fr/",
			"https://www.credit-cooperatif.coop/",
			"https://www.credit-du-nord.fr/",
			"https://www.credit-et-banque.com/",
			"https://www.creditfoncier.fr/",
			"https://www.creditmutuel.fr/",
			"https://www.culturebanque.com/",
			"https://www.diac.fr/",
			"https://www.direct-assurance.fr/",
			"https://www.economie.gouv.fr/",
			"https://www.empruntis.com/epargne/",
			"https://www.en-bourse.fr/",
			"https://www.eurofil.com/",
			"https://www.fortuneo.fr/",
			"https://www.francetransactions.com/",
			"https://www.gan.fr/",
			"https://www.groupama.fr/",
			"https://www.hellobank.fr/",
			"https://www.home.saxo/fr-fr/",
			"https://www.hsbc.fr/",
			"https://www.impots.gouv.fr/portail/",
			"https://www.ing.fr/banque-en-ligne/",
			"https://www.labanquepostale.fr/",
			"https://www.lcl.fr/",
			"https://www.lerevenu.com/",
			"https://www.lesechos.fr/finance-marches/",
			"https://www.lesfurets.com/",
			"https://www.lolivier.fr/",
			"https://www.macif.fr/assurance/particuliers",
			"https://www.mae.fr/",
			"https://www.maif.fr/",
			"https://www.matmut.fr/",
			"https://www.mma.fr/",
			"https://www.monabanq.com/fr/index.html",
			"https://www.mon-epargne.com/",
			"https://www.montepaschi-banque.fr/fr/",
			"https://www.natixis.com/",
			"https://www.oney.fr/",
			"https://www.orangebank.fr/",
			"https://www.ouest-france.fr/economie/banques-finance/",
			"https://www.palatine.fr/",
			"https://www.panorabanques.com/",
			"https://www.probtp.com/",
			"https://www.psabanque.fr/",
			"https://www.quechoisir.org/thematique-banque-credit-t111/",
			"https://www.revolut.com/fr-FR/",
			"https://www.service-public.fr/particuliers/vosdroits/N19803",
			"https://www.smc.fr/",
			"https://www.societegenerale.fr/",
			"https://www.sofinco.fr/",
			"https://www.toutsurmesfinances.com/",
			"https://www.tradingsat.com/",
			"https://www.usine-digitale.fr/banque/",
			"https://www.younited-credit.com/"]

# --- Utility functions ---

def _memory_size(obj, seen=None):
    size = sys.getsizeof(obj)
    if seen is None:
        seen = set()
    obj_id = id(obj)
    if obj_id in seen:
        return 0
    seen.add(obj_id)
    if isinstance(obj, dict):
        size += sum([memory_size(v, seen) for v in obj.values()])
        size += sum([memory_size(k, seen) for k in obj.keys()])
    elif hasattr(obj, '__dict__'):
        size += memory_size(obj.__dict__, seen)
    elif hasattr(obj, '__iter__') and not isinstance(obj, (str, bytes, bytearray)):
        size += sum([memory_size(i, seen) for i in obj])
    return size

# OTHER OPTION specific to pandas dataframes
# https://www.dataquest.io/blog/pandas-big-data/
# df.info(memory_usage="deep")

def _file_size(filepath):
    statinfo = os.stat(filepath)
    return statinfo.st_size

def _format_size_mb(size):
    return int(size / 1024.0 / 102.4) / 10.0
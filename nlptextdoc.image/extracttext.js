class PageElement {
    constructor(tagName, classNames, boundingBox) {
        this.tagName = tagName;
        this.classNames = classNames;
        this.boundingBox = boundingBox;
        this.children = null;
    }

    appendChild(child) {
        if (this.children == null) {
            this.children = [];
        }
        this.children.push(child);
    }

    hasText() {
        return "text" in this;
    }

    hasOneChild() {
        return this.children != null && this.children.length == 1;
    }
}

class TextBlock extends PageElement {
    constructor(tagName, classNames, text, words) {
        var boundingBox = getBoundingBoxAround(words);
        super(tagName, classNames, boundingBox);
        this.text = text;
        this.lines = createLinesFromWords(words);
        if (window.drawRectangles) {
            console.log("> " + this.tagName + "." + this.classNames + " " + this.text);
            drawRectangle(this.boundingBox, "white", 0, "solid", 1, "red");
        }
    }
}

class TextLine {
    constructor(firstWord) {
        this.text = firstWord.text;
        this.boundingBox = firstWord.boundingBox;
        this.words = [];
        this.words.push(firstWord);
    }

    appendWord(nextWord) {
        this.words.push(nextWord);
        this.boundingBox = getBoundingBoxAround([this, nextWord]);
        this.text = this.text + " " + nextWord.text;
    }

    end() {
        if (window.drawRectangles) {
            drawRectangle(this.boundingBox, "white", 0, "solid", 1, "green");
        }
    }
}

class Word {
    constructor(text, letters) {
        this.text = text;
        this.boundingBox = getBoundingBoxAround(letters);
        this.letters = letters;
        if (window.drawRectangles) {
            drawRectangle(this.boundingBox, "white", 0, "solid", 1, "blue");
        }
    }
}

class Letter {
    constructor(char, boundingBox) {
        this.char = char;
        this.boundingBox = boundingBox;
        if (window.drawRectangles) {
            drawRectangle(this.boundingBox, window.altLetter % 2 == 0 ? "#AAAAFF" : "#5555FF", 0.4, "none", 1, "black");
            window.altLetter++;
        }
    }
}

class TextLabel extends PageElement {
    constructor(tagName, classNames, boundingBox, text) {
        super(tagName, classNames, boundingBox);
        this.text = text;
        drawRectangle(this.boundingBox, "yellow", 0.4, "solid", 1, "orange");
    }
}

class BoundingBox {
    constructor(x, y, width, height) {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }
}

function getBoundingBoxAround(objs) {
    var left = Number.MAX_VALUE;
    var right = Number.MIN_VALUE;
    var top = Number.MAX_VALUE;
    var bottom = Number.MIN_VALUE;
    for (var i = 0; i < objs.length; i++) {
        var box = objs[i].boundingBox;
        if (left > box.x) left = box.x;
        if (top > box.y) top = box.y;
        if ((box.x + box.width) > right) right = box.x + box.width;
        if ((box.y + box.height) > bottom) bottom = box.y + box.height;
    }
    return new BoundingBox(left, top, right - left, bottom-top);
}

function createPageElement(domElt, type=null, text=null, words=null) {
    var pageElt = null;
    if (type == "TextBlock") {
        pageElt = new TextBlock(domElt.tagName, domElt.className, text, words);
    } else if (type == "TextLabel") {
        pageElt = new TextLabel(domElt.tagName, domElt.className, getBoundingBox(domElt), text);
    } else {
        pageElt = new PageElement(domElt.tagName, domElt.className, getBoundingBox(domElt));
    }
    window.pageElements.set(domElt, pageElt);
    // Recursively create parents chain
    var domParent = domElt.parentElement;
    if (domParent != null) {
        var pageParent = null;
        if (window.pageElements.has(domParent)) {
            pageParent = window.pageElements.get(domParent);
        } else {
            pageParent = createPageElement(domParent);
        }
        pageParent.appendChild(pageElt);
    }
    return pageElt;
}

function prunePageElements(pageEltsList) {
    for (var i = 0; i < pageEltsList.length ; i++) {
        var pageElt = pageEltsList[i];
        var replacePageElt = pageElt;
        while (!replacePageElt.hasText() && replacePageElt.hasOneChild()) {
            replacePageElt = replacePageElt.children[0];
        }
        if (replacePageElt != pageElt) {
            pageEltsList[i] = replacePageElt;
        }
        if (replacePageElt.children != null) {
            prunePageElements(replacePageElt.children);
        }
    }
}

function getBoundingBox(elt) {
    var rect = elt.getBoundingClientRect();
    return new BoundingBox(Math.round(rect.left), Math.round(rect.top), Math.round(rect.width), Math.round(rect.height));
}

function createLinesFromWords(words) {
    var lines = [];
    var lastLine = null;
    for (var i = 0; i < words.length; i++) {
        var word = words[i];
        if (lastLine == null) {
            lastLine = new TextLine(word);
            lines.push(lastLine);
        } else {
            if (word.boundingBox.y > (lastLine.boundingBox.y + lastLine.boundingBox.height / 2)) {
                lastLine.end();
                lastLine = new TextLine(word);
                lines.push(lastLine);
            } else {
                lastLine.appendWord(word);
            }
        }
    }
    lastLine.end();
    return lines;
}

String.prototype.capitalize = function () {
    return this.replace(/(?:^|\s)\S/g, function (a) { return a.toUpperCase(); });
};

function extractText(debug=false) {
    console.log("Starting DOM traversal ...");
    window.drawRectangles = debug;
    window.altLetter = 0;
    window.pageElements = new Map();
    var htmlNode = document.children[0];
    window.pageRoot = createPageElement(htmlNode);
    window.pageElements.set(htmlNode, window.pageRoot);
    visitBlock(document.body);
    prunePageElements(window.pageRoot.children);
    console.log("OK, result ready");
    return JSON.stringify(window.pageRoot);
}

function visitBlock(node) {
    if (isVisible(node)) {
        if (node.tagName == "INPUT" && node.type == "text" && node.placeholder != "") {
            createPageElement(node, "TextLabel", node.placeholder);
        } else if (node.tagName=="INPUT" && (node.type=="submit" || node.type=="button") && node.value!="") {
            createPageElement(node, "TextLabel", node.value);
        } else if ((node.tagName == "IFRAME") && (node.contentDocument != null)) {
            visitBlock(node.contentDocument.body);
        } else if (node.hasChildNodes()) {
            var inlineContext = [];
            var childNodes = getChildNodesWithPseudoElements(node);
            for (var i = 0; i < childNodes.length; i++) {
                child = childNodes[i];
                if (child.nodeType == Node.TEXT_NODE) {
                    pushTextNodeToInlineContext(node, child, inlineContext);
                } else if (child.nodeType == Node.ELEMENT_NODE) {
                    if (child.tagName != "SCRIPT") {
                        var childStyle = window.getComputedStyle(child, null);
                        var displayStyle = childStyle.getPropertyValue('display');
                        if (displayStyle && displayStyle=="inline") {
                            visitInline(child, inlineContext);
                        } else {
                            inlineContext = writeAndResetInlineContext(node, inlineContext);
                            visitBlock(child);
                        }
                    }
                }
            }
            writeAndResetInlineContext(node, inlineContext);
        }
    } else {
        for (var i = 0; i < node.childNodes.length; i++) {
            child = node.childNodes[i];
            if (child.nodeType == Node.ELEMENT_NODE) {
                visitBlock(child);
            }
        }
    }
}

function visitInline(node, inlineContext) {
    if (isVisible(node) && node.hasChildNodes()) {
        var childNodes = getChildNodesWithPseudoElements(node);
        for (var i = 0; i < childNodes.length; i++) {
            child = childNodes[i];
            if (child.nodeType == Node.TEXT_NODE) {
                pushTextNodeToInlineContext(node, child, inlineContext);
            } else if (child.nodeType == Node.ELEMENT_NODE) {
                visitInline(child, inlineContext);
            }
        }
    }
}

function pushTextNodeToInlineContext(node, child, inlineContext) {
    if (child.nodeValue.length > 0) {
        var nodeStyle = window.getComputedStyle(node, null);
        var textTransformStyle = nodeStyle.getPropertyValue('text-transform');
        if (textTransformStyle == "uppercase") {
            child.nodeValue = child.nodeValue.toUpperCase();
        } else if (textTransformStyle == "lowercase") {
            child.nodeValue = child.nodeValue.toLowerCase();
        } else if (textTransformStyle == "capitalize") {
            child.nodeValue = child.nodeValue.capitalize();
        }
        inlineContext.push(child);
    }
}

function writeAndResetInlineContext(node, inlineContext) {
    if (inlineContext.length == 0) {
        return inlineContext;
    } else {
        var lastCharWasSpace = true;
        var text = "";
        var words = [];
        for (var i = 0; i < inlineContext.length; i++) {
            var textNode = inlineContext[i];
            var textNodeValue = textNode.nodeValue;
            var wordStartIndex = 0;
            for (var j = 0; j < textNodeValue.length; j++) {
                var char = textNodeValue.charAt(j);               
                if (char == " " || char == "\xa0" || char == "\t" || char == "\n" || char == "\r") {
                    if (lastCharWasSpace) {
                        // ignore repeated spaces
                        wordStartIndex++;
                    } else {
                        var word = createWord(textNode, wordStartIndex, j);
                        if (word != null) {
                            words.push(word);
                        }
                        text += " ";
                        lastCharWasSpace = true;
                        wordStartIndex = j + 1;
                    }
                } else {
                    text += char;
                    lastCharWasSpace = false;
                }
            }
            if (wordStartIndex < (textNodeValue.length -1)) {
                var word = createWord(textNode, wordStartIndex, textNodeValue.length);
                if (word != null) {
                    words.push(word);
                }
            }
        }
        if (words.length > 0) {
            var textBlock = createPageElement(node, "TextBlock", text, words);
        }        
        return [];
    }
}

function createWord(textNode, wordStartIndex, wordEndIndex) {
    var word = null;
    var wordTxt = textNode.nodeValue.substring(wordStartIndex, wordEndIndex);
    if (wordTxt.trim().length > 0) {
        // Create letters
        var letters = []
        var range = document.createRange();
        if (textNode.parentNode != null) {
            for (var i = wordStartIndex; i < wordEndIndex; i++) {
                range.setStart(textNode, i);
                range.setEnd(textNode, i + 1);
                var box = getBoundingBox(range);
                var letter = new Letter(textNode.nodeValue.charAt(i),box);
                letters.push(letter);
            }
            // Special case for pseudo-elements
        } else if (textNode.parent != null) {
            var parentRect = textNode.parent.getBoundingClientRect();
            var range = document.createRange();
            range.setStart(textNode.parent, 0);
            range.setEnd(textNode.parent, 1);
            var textRect = range.getBoundingClientRect();
            var pseudoElementWidth = parentRect.width - textRect.width;
            var box = null;
            if (parentRect.x == textRect.x) {
                box = new BoundingBox(parentRect.x + parentRect.width - pseudoElementWidth, parentRect.y, pseudoElementWidth, parentRect.height);
            } else {
                box = new BoundingBox(parentRect.x, parentRect.y, pseudoElementWidth, parentRect.height);
            }
            var letter = new Letter(textNode.nodeValue.trim(),box);
            letters.push(letter);
        }
        word = new Word(wordTxt, letters);
    }
    return word;
}

function getChildNodesWithPseudoElements(node) {
    var childNodesWithPseudoElements = [];
    var beforeTxt = window.getComputedStyle(node, "::before").getPropertyValue("content");
    if (beforeTxt != "none" && isNotIcon(beforeTxt)) {
        var beforeNode = document.createTextNode(beforeTxt.substring(1, beforeTxt.length - 1) + " ");
        beforeNode.parent = node;
        childNodesWithPseudoElements.push(beforeNode);
    }
    for (var i = 0; i < node.childNodes.length; i++) {
        child = node.childNodes[i];
        childNodesWithPseudoElements.push(child);
    }
    afterTxt = window.getComputedStyle(node, "::after").getPropertyValue("content");
    if (afterTxt != "none" && isNotIcon(afterTxt)) {
        var afterNode = document.createTextNode(" " + afterTxt.substring(1, afterTxt.length - 1));
        afterNode.parent = node;
        childNodesWithPseudoElements.push(afterNode);
    }
    return childNodesWithPseudoElements;
}

function isNotIcon(text) {
    for (var i = 0; i < text.length; i++) {
        var charCode = text.charCodeAt(i);
        // Filter on valid ISO8859-1 codes
        if (!((charCode >= 32 && charCode <=127) || (charCode >= 160 && charCode <= 255))) {
            return false;
        }
    }
    return true;
}

function isVisible(elem) {
    if (!(elem instanceof Element)) return false;
    const style = getComputedStyle(elem);
    if (style.display === 'none') return false;
    if (style.visibility !== 'visible') return false;
    if (style.opacity < 0.1) return false;
    if (elem.offsetWidth + elem.offsetHeight + elem.getBoundingClientRect().height +
        elem.getBoundingClientRect().width === 0) {
        return false;
    }
    const elemCenter = {
        x: elem.getBoundingClientRect().left + elem.offsetWidth / 2,
        // Warning : 2.2 important just below
        y: elem.getBoundingClientRect().top + elem.offsetHeight / 2.2
    };
    if (!isFinite(elemCenter.x) || elemCenter.x < 0) return false;
    if (elemCenter.x > (document.documentElement.clientWidth || window.innerWidth)) return false;
    if (!isFinite(elemCenter.y) || elemCenter.y < 0) return false;
    if (elemCenter.y > (document.documentElement.clientHeight || window.innerHeight)) return false;
    let pointContainer = document.elementFromPoint(elemCenter.x, elemCenter.y);
    if (pointContainer != null) {
        do {
            if (pointContainer === elem) return true;
        } while (pointContainer = pointContainer.parentNode);
    }
    return false;
}

function drawRectangle(box, color, opacity, borderStyle, borderWidth, borderColor) {
    var overlay = document.createElement("div");
    overlay.style.position = "fixed";
    overlay.style.left = box.x + "px";
    overlay.style.top = box.y + "px";
    overlay.style.width = box.width + "px";
    overlay.style.height = box.height + "px";
    if (opacity > 0) {
        overlay.style.backgroundColor = color;
        overlay.style.opacity = opacity;
    }
    overlay.style.borderStyle = borderStyle;
    overlay.style.borderWidth = borderWidth + "px";
    overlay.style.borderColor = borderColor;
    overlay.style.zIndex = 100000;
    document.body.append(overlay);
    return overlay;
}
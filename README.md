# AI Governance Engineering — Quering a DB table

**This project first part implements only the initial validation stage of the general project.**


## Overview

This project explores an **AI Governance Engineering** approach using a simple AI-assisted database application as example.

The application allows users to submit natural-language requests about a specified database table. AI agents will interpret these requests, generate appropriate SQL queries, execute them, and provide the results to the user.

For this example, the application is limited to a single database table, and it only supports read-only queries. The goal is to demonstrate how governance can be embedded in the application's code and workflow.

The schema of the table is also provided with semantic description of every field, which is used to help the AI agent understand the meaning of the request and the table's structure which it will be used in generating the SQL query.

This schema was automatically generated in the previous project: https://github.com/onesimoh2/AgentFrmDBfieldDiscovery01

Governance is intended to be embedded throughout the application's code and workflow, rather than treated as a separate auditing step.


## Initial Validation

The `InitialValidation` module determines whether a user's request can proceed before further processing. It:

1. **Evaluates the request** using the Microsoft Decision API to assess its intent, completeness, permitted scope, and potential attempts to bypass governance controls.
2. **Checks database permissions** to verify that the current database user has the required read access to the target table.
3. **Handles the governance decision** by approving the request, requesting clarification, escalating concerns, or reporting an error.
4. **Records governance events** in a database to support traceability and subsequent review.

## AI Governance Engineering

The underlying idea is to combine **rule-based controls with semantic constraints**.

Rule-based controls enforce explicit requirements, such as database permissions and operational restrictions. Semantic evaluation addresses aspects that are harder to express as fixed rules, including user intent, the meaning of a request, its suitability for the application's purpose, and possible attempts to manipulate the agent.

Neither approach is sufficient on its own. Rules cannot capture every relevant aspect of meaning, while semantic evaluation cannot guarantee that every unsafe or inappropriate request will be detected.

AI Governance Engineering combines these complementary approaches to build governance into the application's operation, from initial validation through SQL generation and execution to the delivery of results.

## Scope and Limitations

This implementation covers **initial request validation only**. It does not, by itself, govern every subsequent agent decision, generated SQL query, or execution outcome. Those stages will require their own appropriate governance controls, embedded in the corresponding application code.

The current governance record is a minimal implementation intended to demonstrate the approach, not a complete production audit record.

## Purpose

This project is a practical starting point for exploring how semantic evaluation and enforceable technical controls can work together to govern AI-assisted database applications throughout their workflow.

Disclosure and documentation in this repository are provided for **educational purposes only**. They are not intended for production use, and they do not constitute professional advice. The code and documentation may contain errors or omissions
